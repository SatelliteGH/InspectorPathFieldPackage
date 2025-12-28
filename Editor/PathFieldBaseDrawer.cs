using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InspectorPathField.Editor
{
    internal class PathFieldBaseDrawer : PropertyDrawer
    {
        private static PathFieldResources _resources;
        private static PathFieldSettings _settings;
        private const float BUTTON_WIDTH = 20f;
        private const float BUTTON_SPACING = 2;
        private const int BUTTON_COUNT = 3;
        private const float SELECTOR_WINDOW_Y_SHIFT = -45;
        private const float BTN_SHIFT = (BUTTON_WIDTH * BUTTON_COUNT) + (BUTTON_SPACING * (BUTTON_COUNT - 1));
        private float _textFieldWidth;

        private GUIStyle _buttonStyle;

        private PathDisplayMode _currentDisplayMode = PathDisplayMode.ShortPath;


        public PathFieldBaseDrawer()
        {
            if (_resources == null)
            {
                _resources = PathFieldResources.GetAssets();
#if UNITY_EDITOR_PATH_FIELD_DEBUG
                if (_resources != null)
                {
                    Debug.Log(($"Asset load done"));
                }
#endif
            }

            if (_settings == null)
            {
                _settings = PathFieldSettings.GetAssets();

#if UNITY_EDITOR_PATH_FIELD_DEBUG
                if (_settings != null)
                {
                    Debug.Log(($"Settings load done"));
                }
#endif
            }
            else
            {
                _currentDisplayMode = _settings.PathDisplayMode;
            }
        }


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(1, 1, 1, 1)
            };


            // Calculate text field size depends on the button size
            _textFieldWidth = position.width - BTN_SHIFT;

            // Setup rectangle for text field
            Rect textFieldRect = new(position.x, position.y, _textFieldWidth, position.height);

            string displayText;
            EditorGUI.BeginChangeCheck();

            // Display mode logic
            if (textFieldRect.Contains(Event.current.mousePosition)) // if mouse hover, show full path and let change
            {
                displayText = property.stringValue;
                property.stringValue = EditorGUI.TextField(textFieldRect, label, displayText);
            }
            else // if no, just show current display mode
            {
                displayText = GetPathForCurrentMode(property.stringValue);
                EditorGUI.TextField(textFieldRect, label, displayText);
            }

            // Goto button
            GotoButton(0, property, ref position);

            // Display mode
            DisplayModeButton(1, property, ref position);

            // Search button
            if (_settings.SearchType == SearchType.ObjectPicker)
            {
                SearchButtonObjectPicker(2, property, ref position, property);
            }
            else if (_settings.SearchType == SearchType.UnitySearch)
            {
                SearchButtonUnitySearch(2, property, ref position, property);
            }

            property.serializedObject.ApplyModifiedProperties();
        }


        private string GetPathForCurrentMode(string originalPath)
        {
            return _currentDisplayMode switch
            {
                PathDisplayMode.ShortPath => RemoveAllTokens(originalPath, _settings.ShortPathTokensToRemove),
                PathDisplayMode.FileName => originalPath.Split("/")[^1],
                _ => originalPath
            };
        }


        private static string RemoveAllTokens(string original, IEnumerable<string> tokens)
        {
            if (tokens == null || !tokens.Any()) return original;
            string[] pathParts = original.Split('/');

            var filteredParts = new List<string>();

            bool shouldRemove = true;

            foreach (string part in pathParts)
            {
                if (shouldRemove && tokens.Contains(part))
                {
                    continue;
                }

                shouldRemove = false;
                filteredParts.Add(part);
            }

            string result = string.Join("/", filteredParts);
            return result;
        }


        private string GetSymbolByDisplayMode()
        {
            return _currentDisplayMode switch
            {
                PathDisplayMode.FullPath => "F",
                PathDisplayMode.ShortPath => "S",
                _ => "N"
            };
        }


        private void GotoButton(int buttonIndex, SerializedProperty assetPath, ref Rect position)
        {
            bool canGoto = !string.IsNullOrEmpty(assetPath.stringValue);

            GUI.enabled = canGoto;

            // Setup rectangle for goto button
            Rect gotoButtonRect = new(position.x + ButtonShift(buttonIndex),
                                      position.y, BUTTON_WIDTH,
                                      position.height);

            // On press action
            if (!GUI.Button(gotoButtonRect,
                            new GUIContent(_resources.GotoButtonTexture, "Go to asset in project"),
                            _buttonStyle))
            {
                GUI.enabled = true;
                return;
            }

            Object obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath.stringValue);

            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
            }

            GUI.enabled = true;
        }


        private void DisplayModeButton(int buttonIndex, SerializedProperty assetPath, ref Rect position)
        {
            Rect displayModeButtonRect = new(position.x + ButtonShift(buttonIndex),
                                             position.y, BUTTON_WIDTH,
                                             position.height);

            if (!GUI.Button(displayModeButtonRect,
                            new GUIContent(GetSymbolByDisplayMode(), "Change path display mode")))
            {
                return;
            }

            _currentDisplayMode = (PathDisplayMode)((int)(_currentDisplayMode + 1) %
                                                    Enum.GetValues(typeof(PathDisplayMode)).Length);
        }


        private void SearchButtonObjectPicker(int buttonIndex, SerializedProperty assetPath,
                                              ref Rect position, SerializedProperty original)
        {
            Rect searchButtonRect = new(position.x + ButtonShift(buttonIndex),
                                        position.y, BUTTON_WIDTH,
                                        position.height);


            int pickerControlID = -1;

            pickerControlID = GUIUtility.GetControlID(FocusType.Passive);
            // On press action
            if (GUI.Button(searchButtonRect,
                           new GUIContent(_resources.SearchButtonTexture, "Open object picker"),
                           _buttonStyle))
            {
                // Show window
                EditorGUIUtility.ShowObjectPicker<Object>(null, false, "", pickerControlID);


                Type objectSelectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ObjectSelector");
                if (objectSelectorType == null) return;

                EditorWindow objectPicker = EditorWindow.GetWindow(objectSelectorType);

                if (objectPicker == null)
                {
                    return;
                }

                // Window position
                Vector2 mousePosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);

                objectPicker.position = new Rect(mousePosition.x,
                                                 mousePosition.y - SELECTOR_WINDOW_Y_SHIFT,
                                                 objectPicker.position.width,
                                                 objectPicker.position.height);

                // Window title
                objectPicker.titleContent = new GUIContent($"Select: {original.displayName}");
            }

            if (Event.current.commandName != "ObjectSelectorUpdated" ||
                EditorGUIUtility.GetObjectPickerControlID() != pickerControlID)
            {
                return;
            }

            Object pickedObject = EditorGUIUtility.GetObjectPickerObject();

            if (pickedObject == null)
            {
                return;
            }

            // Save path to asset
            string path = AssetDatabase.GetAssetPath(pickedObject);
            assetPath.stringValue = path;
        }


    private void SearchButtonUnitySearch(int buttonIndex, SerializedProperty assetPath,
                                        ref Rect position, SerializedProperty original)
    {
        Rect searchButtonRect = new(position.x + ButtonShift(buttonIndex),
                                    position.y, BUTTON_WIDTH,
                                    position.height);

        if (!GUI.Button(searchButtonRect, _resources.SearchButtonTexture, _buttonStyle))
            return;

        var targetObject = original.serializedObject.targetObject;
        var field = targetObject.GetType()
            .GetField(original.name, System.Reflection.BindingFlags.Public | 
                                    System.Reflection.BindingFlags.NonPublic | 
                                    System.Reflection.BindingFlags.Instance);

        Type defaultType = null;
        if (field != null)
        {
            var attr = field.GetCustomAttributes(typeof(PathFieldAttribute), true)
                            .FirstOrDefault() as PathFieldAttribute;
            if (attr != null)
            {
                defaultType = attr.DefaultSerchType;
            }
        }

        string searchQuery = _settings.SearchQuery;
        if (defaultType != null)
        {
            searchQuery += $" t:{defaultType.Name}";
        }

        SearchContext context = SearchService.CreateContext("asset", searchQuery);

        SearchViewState state = SearchViewState.CreatePickerState(
            $"Select: {original.displayName}",
            context,
            ProceedSelection,
            flags: _settings.SearchViewFlags
        );

        SearchService.ShowPicker(state);

        void ProceedSelection(SearchItem obj, bool i)
        {
            if (GlobalObjectId.TryParse(obj.value as string, out GlobalObjectId id))
            {
                assetPath.stringValue = AssetDatabase.GUIDToAssetPath(id.assetGUID);
                assetPath.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                throw new Exception($"Object {obj} cannot be found or loaded");
            }
        }
    }


        private float ButtonShift(int buttonIndex)
        {
            return _textFieldWidth + (BUTTON_WIDTH * buttonIndex) + (BUTTON_SPACING * (buttonIndex + 1));
        }
    }
}