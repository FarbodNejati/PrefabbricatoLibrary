using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Search;
using UnityEngine;

namespace Farbod.Prefabbricato.Backend
{
    // =====================================================================
    // QUERY (configuration + evaluation, single entry point)
    // =====================================================================

    /// <summary>
    /// A composable query over the asset index.
    /// Every filter (text, labels, path) is a component that can be configured
    /// independently; all ACTIVE filters are combined with AND.
    /// Each assets window owns one Query instance.
    /// </summary>
    internal class Query
    {
        // ----- Filter components (configuration lives here) -----
        public TextQueryFilter Text { get; } = new TextQueryFilter();
        public LabelQueryFilter Labels { get; } = new LabelQueryFilter();
        public PathQueryFilter Paths { get; } = new PathQueryFilter();

        // ----- Sorting -----
        public QueryResult.QuerySortMode SortMode { get; set; } = QueryResult.QuerySortMode.Relevance;

        private readonly List<IQueryFilter> filters = new List<IQueryFilter>();
        private readonly List<IIndexFilter> indexFilters = new List<IIndexFilter>();
        private readonly List<IPredicateFilter> predicateFilters = new List<IPredicateFilter>();

        protected List<PrefabData> AssetDatabase => AssetIndex.PrefabDataList;

        internal Query()
        {
            AddFilter(Text);
            AddFilter(Labels);
            AddFilter(Paths);
        }

        // -----------------------------------------------------------------
        // Convenience configuration (forwards to the components)
        // -----------------------------------------------------------------

        /// <summary>Free text search (tokenised, scored).</summary>
        public string SearchText
        {
            get => Text.Input;
            set => Text.Input = value;
        }

        public Query SetText(string input)
        {
            Text.Input = input;
            return this;
        }

        public Query SetLabels(
            IEnumerable<string> labels,
            LabelQueryMode mode = LabelQueryMode.Union)
        {
            Labels.Set(labels, mode);
            return this;
        }

        public Query SetPaths(
            IEnumerable<string> paths,
            PathMatchMode mode = PathMatchMode.FolderRecursive)
        {
            Paths.Set(paths, mode);
            return this;
        }

        public Query SetSortMode(QueryResult.QuerySortMode sortMode)
        {
            SortMode = sortMode;
            return this;
        }

        /// <summary>True if at least one filter currently restricts the results.</summary>
        public bool HasActiveFilters
        {
            get
            {
                for (int i = 0; i < filters.Count; i++)
                    if (filters[i].IsActive)
                        return true;
                return false;
            }
        }

        /// <summary>Clear every filter and restore the default sort mode.</summary>
        public Query Reset()
        {
            for (int i = 0; i < filters.Count; i++)
                filters[i].Reset();

            SortMode = QueryResult.QuerySortMode.Relevance;
            return this;
        }

        /// <summary>
        /// Register an additional filter component.
        /// A filter participates by implementing IIndexFilter and/or IPredicateFilter.
        /// </summary>
        internal void AddFilter(IQueryFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            filters.Add(filter);

            if (filter is IIndexFilter indexFilter)
                indexFilters.Add(indexFilter);

            if (filter is IPredicateFilter predicateFilter)
                predicateFilters.Add(predicateFilter);
        }

        // -----------------------------------------------------------------
        // Evaluation
        // -----------------------------------------------------------------

        internal QueryResult Evaluate()
        {
            if (!AssetIndex.IsIndexed)
                return new QueryResult(this, Array.Empty<PrefabData>());

            // Stage 1: index-backed filters narrow a GUID set (cheap, no per-asset work).
            QueryContext context = new QueryContext();

            for (int i = 0; i < indexFilters.Count; i++)
            {
                IIndexFilter filter = indexFilters[i];

                if (!filter.IsActive)
                    continue;

                filter.Apply(context);

                if (context.IsEmpty)
                    return new QueryResult(this, Array.Empty<PrefabData>());
            }

            // Resolve GUIDs -> PrefabData in one pass over the database.
            IReadOnlyList<PrefabData> results = context.Resolve(AssetDatabase);

            // Stage 2: predicate filters run only over the surviving assets.
            results = ApplyPredicates(results);

            return new QueryResult(this, results, context.Scores)
                .SortBy(SortMode);
        }

        private IReadOnlyList<PrefabData> ApplyPredicates(IReadOnlyList<PrefabData> input)
        {
            List<IPredicateFilter> active = null;

            for (int i = 0; i < predicateFilters.Count; i++)
            {
                if (predicateFilters[i].IsActive)
                    (active ??= new List<IPredicateFilter>(predicateFilters.Count))
                        .Add(predicateFilters[i]);
            }

            if (active == null)
                return input;

            List<PrefabData> output = new List<PrefabData>(input.Count);

            for (int i = 0; i < input.Count; i++)
            {
                PrefabData data = input[i];
                bool accepted = true;

                for (int f = 0; f < active.Count; f++)
                {
                    if (active[f].Matches(data))
                        continue;

                    accepted = false;
                    break;
                }

                if (accepted)
                    output.Add(data);
            }

            return output;
        }

        public override string ToString()
        {
            string folder = null;
            if (Paths.Paths.FirstOrDefault() is string path && !string.IsNullOrWhiteSpace(path))
            {
                path = path.TrimEnd(
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar);

                folder = Path.GetFileName(path);
            }

            //Search
            if (!string.IsNullOrWhiteSpace(Text.Input))
            {
                string value = $"{Text.Input} - In {folder??"Library"}";
                return value;
            }

            //Labels
            if (Labels.Labels?.Count() > 0)
            {
                string value = "#" + string.Join(" #", Labels.Labels);
                if (folder != null)
                    return value + $" - In {folder}";

                return value;
            }

            //Folder
            if(folder != null)
                return folder;

            //Fallback
            return "Library";
        }
    }


    // =====================================================================
    // RESULT
    // =====================================================================

    internal class QueryResult
    {
        internal enum QuerySortMode
        {
            Relevance,
            NameAscending,
            NameDescending,
            PathAscending,
            PathDescending,
        }

        public readonly Query Query;
        public IReadOnlyCollection<PrefabData> Results { get; private set; }

        private IReadOnlyDictionary<string, int> relevance;

        // The sort mode Results is currently ordered by (null = unsorted).
        private QuerySortMode? appliedSortMode;

        internal QueryResult(
            Query query,
            IReadOnlyList<PrefabData> results,
            IReadOnlyDictionary<string, int> relevance = null)
        {
            Query = query;
            Results = results;
            this.relevance = relevance;
        }

        /// <summary>
        /// Regenerate this query result, to match the current asset index.
        /// Uses the query's current filters and sort mode.
        /// </summary>
        internal QueryResult ReEvaluate()
        {
            QueryResult evaluated = Query.Evaluate();

            Results = evaluated.Results;
            relevance = evaluated.relevance;
            appliedSortMode = evaluated.appliedSortMode;

            return this;
        }

        /// <summary>
        /// Sort the results. The mode is also stored on the query, so
        /// it survives ReEvaluate() and filter changes.
        /// </summary>
        public QueryResult SortBy(QuerySortMode sortMode)
        {
            Query.SortMode = sortMode;

            if (appliedSortMode == sortMode)
                return this;

            appliedSortMode = sortMode;

            if (Results.Count <= 1)
                return this;

            // Never sort the shared asset database in place.
            List<PrefabData> sorted = Results.ToList();

            switch (sortMode)
            {
                case QuerySortMode.Relevance:
                    {
                        if (relevance == null)
                        {
                            sorted.Sort(CompareNames);
                            break;
                        }

                        sorted.Sort((a, b) =>
                        {
                            int aScore = relevance.TryGetValue(a.guid, out int aValue) ? aValue : 0;
                            int bScore = relevance.TryGetValue(b.guid, out int bValue) ? bValue : 0;

                            int comparison = bScore.CompareTo(aScore);

                            return comparison != 0
                                ? comparison
                                : CompareNames(a, b);
                        });

                        break;
                    }

                case QuerySortMode.NameAscending:
                    sorted.Sort(CompareNames);
                    break;

                case QuerySortMode.NameDescending:
                    sorted.Sort((a, b) => CompareNames(b, a));
                    break;

                case QuerySortMode.PathAscending:
                    sorted.Sort(ComparePaths);
                    break;

                case QuerySortMode.PathDescending:
                    sorted.Sort((a, b) => ComparePaths(b, a));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(sortMode),
                        sortMode,
                        null);
            }

            Results = sorted;

            return this;
        }

        private static int CompareNames(PrefabData a, PrefabData b)
            => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);

        private static int ComparePaths(PrefabData a, PrefabData b)
            => string.Compare(a.assetPath, b.assetPath, StringComparison.OrdinalIgnoreCase);
    }

    // =====================================================================
    // FILTER COMPONENT ARCHITECTURE
    // =====================================================================

    /// <summary>Base contract for every filter component.</summary>
    internal interface IQueryFilter
    {
        /// <summary>Inactive filters are skipped entirely.</summary>
        bool IsActive { get; }

        /// <summary>Clear this filter's configuration.</summary>
        void Reset();
    }

    /// <summary>
    /// A filter backed by the asset index (token / label dictionaries).
    /// Narrows the GUID candidate set and contributes relevance.
    /// </summary>
    internal interface IIndexFilter : IQueryFilter
    {
        void Apply(QueryContext context);
    }

    /// <summary>
    /// A filter evaluated directly against asset data (e.g. its path).
    /// Runs after index filters, only on the surviving assets.
    /// </summary>
    internal interface IPredicateFilter : IQueryFilter
    {
        bool Matches(PrefabData data);
    }

    /// <summary>
    /// Shared state while evaluating a query: the candidate GUID set
    /// (null = unrestricted) and the accumulated relevance scores.
    /// </summary>
    internal sealed class QueryContext
    {
        private HashSet<string> candidates; // null = every asset
        private Dictionary<string, int> scores;

        public bool IsEmpty => candidates != null && candidates.Count == 0;

        public IReadOnlyDictionary<string, int> Scores => scores;

        /// <summary>
        /// Intersect the candidates with the given matches (guid -> score)
        /// and add their scores to the running relevance.
        /// </summary>
        public void Intersect(Dictionary<string, int> matches)
        {
            if (matches.Count == 0)
            {
                Clear();
                return;
            }

            if (candidates == null)
            {
                candidates = new HashSet<string>(matches.Keys);
            }
            else
            {
                candidates.RemoveWhere(guid => !matches.ContainsKey(guid));

                if (candidates.Count == 0)
                    return;
            }

            scores ??= new Dictionary<string, int>(candidates.Count);

            foreach (string guid in candidates)
            {
                int add = matches[guid];

                scores[guid] = scores.TryGetValue(guid, out int current)
                    ? current + add
                    : add;
            }
        }

        /// <summary>Mark the result as empty.</summary>
        public void Clear()
        {
            candidates = new HashSet<string>();
        }

        /// <summary>
        /// Convert the candidate GUIDs to PrefabData in a single pass over the
        /// database. When nothing restricted the set, the database itself is returned.
        /// </summary>
        public IReadOnlyList<PrefabData> Resolve(List<PrefabData> database)
        {
            if (candidates == null)
                return database;

            List<PrefabData> results = new List<PrefabData>(candidates.Count);

            for (int i = 0; i < database.Count; i++)
            {
                PrefabData data = database[i];

                if (candidates.Contains(data.guid))
                    results.Add(data);
            }

            return results;
        }
    }

    // ---------------------------------------------------------------------
    // TEXT FILTER
    // ---------------------------------------------------------------------

    /// <summary>Free-text search over name tokens and labels, with relevance scoring.</summary>
    internal sealed class TextQueryFilter : IIndexFilter
    {
        // The characters which split the query into tokens.
        private static readonly char[] TOKEN_SPLITTER =
        {
            ' ', '\t', '\r', '\n', '-', '_', '/', '\\', '.', ',', ';', ':'
        };

        // Name matches should always have significantly more weight than label matches.
        private const int NAME_MATCH_SCORE = 100;
        private const int LABEL_MATCH_SCORE = 10;

        // Partial matches should be weaker than exact matches.
        private const int PARTIAL_NAME_MATCH_SCORE = 50;
        private const int PARTIAL_LABEL_MATCH_SCORE = 5;

        private string input = string.Empty;
        private string[] tokens = Array.Empty<string>();

        /// <summary>Raw user input. Tokenised once when set, not on every evaluation.</summary>
        public string Input
        {
            get => input;
            set
            {
                input = value ?? string.Empty;
                tokens = Tokenize(input);
            }
        }

        public bool IsActive => tokens.Length > 0;

        public void Reset() => Input = string.Empty;

        public void Apply(QueryContext context)
        {
            // GUID -> relevance score
            Dictionary<string, int> scores = new Dictionary<string, int>();

            foreach (string token in tokens)
            {
                // NAME MATCH
                foreach (KeyValuePair<string, HashSet<string>> entry in AssetIndex.TokenToAssetIndex)
                {
                    string indexedToken = entry.Key;

                    if (!indexedToken.Contains(token, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int score = string.Equals(indexedToken, token, StringComparison.OrdinalIgnoreCase)
                        ? NAME_MATCH_SCORE
                        : PARTIAL_NAME_MATCH_SCORE;

                    foreach (string guid in entry.Value)
                        AddScore(scores, guid, score);
                }

                // LABEL / TAG MATCH
                foreach (KeyValuePair<string, HashSet<string>> entry in AssetIndex.LabelToAssetIndex)
                {
                    string indexedLabel = entry.Key;

                    if (!indexedLabel.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int score = string.Equals(indexedLabel, token, StringComparison.OrdinalIgnoreCase)
                        ? LABEL_MATCH_SCORE
                        : PARTIAL_LABEL_MATCH_SCORE;

                    foreach (string guid in entry.Value)
                        AddScore(scores, guid, score);
                }
            }

            context.Intersect(scores);
        }

        private static void AddScore(Dictionary<string, int> scores, string guid, int score)
        {
            scores[guid] = scores.TryGetValue(guid, out int current)
                ? current + score
                : score;
        }

        private static string[] Tokenize(string text)
        {
            return text
                .Split(TOKEN_SPLITTER, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length > 0)
                .Distinct()
                .ToArray();
        }
    }

    // ---------------------------------------------------------------------
    // LABEL FILTER
    // ---------------------------------------------------------------------

    internal enum LabelQueryMode
    {
        /// <summary>Asset has at least one of the labels.</summary>
        Union,

        /// <summary>Asset has all of the labels.</summary>
        Intersection
    }

    /// <summary>Filter by exact labels. Each matched label adds to the relevance.</summary>
    internal sealed class LabelQueryFilter : IIndexFilter
    {
        // Low on purpose: text matches dominate, label count breaks ties.
        private const int LABEL_FILTER_SCORE = 1;

        private string[] labels = Array.Empty<string>();

        public LabelQueryMode Mode { get; set; } = LabelQueryMode.Union;

        public IReadOnlyList<string> Labels => labels;

        public bool IsActive => labels.Length > 0;

        public void Set(IEnumerable<string> newLabels, LabelQueryMode mode = LabelQueryMode.Union)
        {
            labels = newLabels?
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Select(label => label.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();

            Mode = mode;
        }

        public void Reset()
        {
            labels = Array.Empty<string>();
            Mode = LabelQueryMode.Union;
        }

        public void Apply(QueryContext context)
        {
            // GUID -> number of requested labels matched.
            Dictionary<string, int> matchCounts = new Dictionary<string, int>();

            foreach (string label in labels)
            {
                if (!AssetIndex.LabelToAssetIndex.TryGetValue(label, out HashSet<string> assets))
                {
                    // Intersection can never be satisfied if a label has no assets.
                    if (Mode == LabelQueryMode.Intersection)
                    {
                        context.Clear();
                        return;
                    }

                    continue;
                }

                foreach (string guid in assets)
                {
                    matchCounts[guid] = matchCounts.TryGetValue(guid, out int count)
                        ? count + 1
                        : 1;
                }
            }

            if (Mode == LabelQueryMode.Intersection)
            {
                int required = labels.Length;

                matchCounts = matchCounts
                    .Where(x => x.Value == required)
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            // Convert match counts to scores.
            Dictionary<string, int> scores = new Dictionary<string, int>(matchCounts.Count);

            foreach (KeyValuePair<string, int> pair in matchCounts)
                scores[pair.Key] = pair.Value * LABEL_FILTER_SCORE;

            context.Intersect(scores);
        }
    }

    // ---------------------------------------------------------------------
    // PATH FILTER
    // ---------------------------------------------------------------------

    internal enum PathMatchMode
    {
        /// <summary>Asset is inside one of the folders, or any of their subfolders.</summary>
        FolderRecursive,

        /// <summary>Asset is directly inside one of the folders.</summary>
        FolderOnly,

        /// <summary>Asset path contains one of the given strings.</summary>
        Contains
    }

    /// <summary>Filter by asset path. Multiple paths are combined with OR.</summary>
    internal sealed class PathQueryFilter : IPredicateFilter
    {
        private string[] paths = Array.Empty<string>();

        public PathMatchMode Mode { get; set; } = PathMatchMode.FolderRecursive;

        public IReadOnlyList<string> Paths => paths;

        public bool IsActive => paths.Length > 0;

        public void Set(IEnumerable<string> newPaths, PathMatchMode mode = PathMatchMode.FolderRecursive)
        {
            Mode = mode; // must be set first: Normalize depends on it

            paths = newPaths?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(Normalize)
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
        }

        public void Reset()
        {
            paths = Array.Empty<string>();
            Mode = PathMatchMode.FolderRecursive;
        }

        public bool Matches(PrefabData data)
        {
            string assetPath = data.assetPath;

            if (string.IsNullOrEmpty(assetPath))
                return false;

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];

                switch (Mode)
                {
                    case PathMatchMode.FolderRecursive:
                        if (assetPath.Length > path.Length
                            && assetPath[path.Length] == '/'
                            && assetPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                            return true;
                        break;

                    case PathMatchMode.FolderOnly:
                        if (assetPath.LastIndexOf('/') == path.Length
                            && assetPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                            return true;
                        break;

                    case PathMatchMode.Contains:
                        if (assetPath.Contains(path, StringComparison.OrdinalIgnoreCase))
                            return true;
                        break;
                }
            }

            return false;
        }

        // Asset paths use '/' separators and have no trailing slash.
        private string Normalize(string path)
        {
            path = path.Trim().Replace('\\', '/');

            return Mode == PathMatchMode.Contains
                ? path
                : path.TrimEnd('/');
        }
    }
}