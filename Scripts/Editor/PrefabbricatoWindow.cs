using Farbod.Prefabbricato.Backend;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    public class PrefabbricatoWindow : EditorWindow
    {
        static readonly string UXML_RESOURCE_PATH = "UXML/PrefabbricatoLibrary";
        static readonly string STYLESHEET_RESOURCE_PATH = "Style/PrefabbricatoLibraryStyle";
        static readonly string WINDOW_ICON_CONTENT = "d_FilterByLabel";
        static readonly string WINDOW_TITLE = "Prefabbricato";
        static readonly Vector2 WINDOW_MIN_SIZE = new(500, 400);

        static readonly string m_GetStartedOverlayUssClassName = "getting-started";
        static readonly string m_GetStartedButtonUssClassName = "getting-started_button";


        private VisualElement m_Root;
        private PrefabbricatoLibraryView m_LibraryView;
        /// <summary>
        /// The menu item available in the editor toolbar for opening this window.
        /// </summary>
        [MenuItem("Tools/Prefabbricato Library")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<PrefabbricatoWindow>();
            var icon = EditorGUIUtility.IconContent(WINDOW_ICON_CONTENT).image;
            wnd.titleContent = new GUIContent(WINDOW_TITLE, icon);
            wnd.minSize = WINDOW_MIN_SIZE;
        }

        /// <summary>
        /// The main function that is called when this window is created.
        /// </summary>
        protected virtual void CreateGUI()
        {
            m_Root = base.rootVisualElement;

            PopulateWindow(m_Root);
            RegisterCallbacks(m_Root);

            ShowStartMenu(m_Root, !CheckStartup(), SelectRootDirectory);

            PrefabbricatoSettings.OnRootChange += () =>
            {
                ShowStartMenu(m_Root, !CheckStartup(), SelectRootDirectory);
            };
        }

        private void SelectRootDirectory()
        {
            PrefabbricatoSettings.SelectLibraryDirectory();
        }

        /// <summary>
        /// Populate the library window with UI Elements.
        /// </summary>
        private void PopulateWindow(VisualElement root)
        {
            //Load main UXML asset
            VisualTreeAsset treeAsset = Resources.Load<VisualTreeAsset>(UXML_RESOURCE_PATH);
            Debug.Assert(treeAsset != null, $"[{WINDOW_TITLE}] UXML Tree Asset not found at {UXML_RESOURCE_PATH}");
            VisualElement treeInstance = treeAsset.Instantiate();
            treeInstance.style.flexGrow = 1;
            root.Add(treeInstance);

            //Load default stylesheet
            StyleSheet style = Resources.Load<StyleSheet>(STYLESHEET_RESOURCE_PATH);
            Debug.Assert(style != null, $"[{WINDOW_TITLE}] Stylesheet not found at {STYLESHEET_RESOURCE_PATH}");
            root.styleSheets.Add(style);


            m_LibraryView = root.Q<PrefabbricatoLibraryView>(name: "library-pane");
            m_LibraryView.SetScanWarningPromptEnabled(!AssetIndex.IsIndexed);
        }

        /// <summary>
        /// Register UI callbacks for user input and functionality.
        /// </summary>
        private void RegisterCallbacks(VisualElement root)
        {
            UpdateAndRedrawOnIndexUpdate();
            //Update Scan needed warning when index is updated.
            AssetIndex.OnIndexUpdate += () => UpdateAndRedrawOnIndexUpdate();
        }
        void UpdateAndRedrawOnIndexUpdate()
        {
            m_LibraryView?.SetScanWarningPromptEnabled(!AssetIndex.IsIndexed);
            m_LibraryView?.LibraryLabelsView?.SetLabels(AssetIndex.Labels);
        }
        private bool CheckStartup()
        {
            return PrefabbricatoSettings.IsSetUp();
        }
        private void ShowStartMenu(VisualElement root, bool show, Action getStarted)
        {
            var overlay = root.Q(className: m_GetStartedOverlayUssClassName);
            if(overlay == null)
            {
                Debug.LogError($"[{WINDOW_TITLE}] Getting started overlay element not found");
                return;
            }
            
            //Set overlay display
            overlay.style.display = show?DisplayStyle.Flex:DisplayStyle.None;

            //If hiding, skip the rest of the code
            if (!show)
                return;

            var btn = overlay.Q<Button>(className: m_GetStartedButtonUssClassName);
            btn.clicked += getStarted;
        }
    }
}