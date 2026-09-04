using Farbod.Prefabbricato.Backend;
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
        private VisualElement m_GettingStarted;

        private SettingsView m_SettingsView;
        private PrefabInspectorView m_Inspector;
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

            //Show start up menu if needed
            ShowStartMenu(m_Root, !CheckStartup());
            
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


            //Hook up "Getting Started"
            m_GettingStarted = root.Q(className: m_GetStartedOverlayUssClassName);
            var btn = m_GettingStarted.Q<Button>(className: m_GetStartedButtonUssClassName);
            btn.clicked += SelectRootDirectory;

            //Hook up settings view
            m_SettingsView = root.Q<SettingsView>();
            m_SettingsView?.Close();

            //Hook up library view
            m_LibraryView = root.Q<PrefabbricatoLibraryView>(name: "library-pane");
            

            //Inspector
            m_Inspector = root.Q<PrefabInspectorView>();
        }

        /// <summary>
        /// Register events and callbacks for functionality.
        /// </summary>
        private void RegisterCallbacks(VisualElement root)
        {
            //--------------------UX---------------------

            //Labels
            m_LibraryView.LibraryLabelsView.onLabelContextMenu += BuildLabelContextMenu;
            m_Inspector.onLabelContextMenu += BuildLabelContextMenu;

            //Settings open button
            if (m_SettingsView != null)
                m_LibraryView.onSettingsButtonClick += m_SettingsView.Open;

            //------------------BACKEND EVENTS  --------------------

            //Show getting started overlay when root becomes invalid
            PrefabbricatoSettings.onLibraryChange += () =>
            {
                ShowStartMenu(m_Root, !CheckStartup());
                m_LibraryView?.SetScanWarningPromptEnabled(true);
            };
            OnIndexUpdate();
            //Update Scan needed warning when index is updated.
            AssetIndex.onIndexUpdate += () => OnIndexUpdate();

            EditorDataManager.onTagColorsChange +=()=> m_LibraryView?.LibraryLabelsView.SetLabels(AssetIndex.Labels);


        }
        private void BuildLabelContextMenu(string label, ContextualMenuPopulateEvent evt)
        {
            var menu = evt.menu;
            menu.AppendAction("Edit Label", e => { 
                m_SettingsView.Open();
                m_SettingsView.SetSearchQuery(label);
            });
        }
        void OnIndexUpdate()
        {
            //Scan needed warning
            m_LibraryView?.SetScanWarningPromptEnabled(!AssetIndex.IsIndexed);
            //Library registered labels
            m_LibraryView?.LibraryLabelsView.SetLabels(AssetIndex.Labels);
            //Library project folders
        }
        private bool CheckStartup() => PrefabbricatoSettings.IsLibrarySetUp();
        private void ShowStartMenu(VisualElement root, bool show)
        {
            //Set overlay display
            m_GettingStarted.style.display = show?DisplayStyle.Flex:DisplayStyle.None;
        }
    }
}