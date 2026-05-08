using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Centralized GUIStyle cache with professional styling
    /// </summary>
    internal static class EditorStylesUtil
    {
        // Spacing constants following 8-pixel grid system
        public static class Spacing
        {
            public const float ITEM_SPACING = 4f;
            public const float SECTION_SPACING = 16f;
            public const float SCROLL_PADDING = 8f;
            
            public static readonly RectOffset BUTTON_PADDING = new RectOffset(16, 16, 8, 8);
            public static readonly RectOffset BUTTON_MARGIN = new RectOffset(4, 4, 2, 2);
            public static readonly RectOffset WINDOW_PADDING = new RectOffset(8, 8, 8, 8);
        }

        // Built-in styles
        public static readonly GUIStyle SearchField = UnityEditor.EditorStyles.toolbarSearchField;
        public static readonly GUIStyle MiniButton = UnityEditor.EditorStyles.miniButton;

        public static readonly GUILayoutOption[] LayoutOptions = new GUILayoutOption[]
            { GUILayout.MaxWidth(150), GUILayout.ExpandWidth(true), GUILayout.Height(20) };

        // Cache for textures and styles
        private static Dictionary<Color, Texture2D> _textureCache = new Dictionary<Color, Texture2D>();
        private static GUIStyle _normalButtonStyle;
        private static GUIStyle _selectedButtonStyle;
        private static GUIStyle _hoverButtonStyle;

        /// <summary>
        /// Detects if Unity is using dark theme
        /// </summary>
        public static bool IsDarkTheme()
        {
            return EditorStyles.label.normal.textColor.r > 0.5f;
        }

        /// <summary>
        /// Gets theme-aware yellow color for selection
        /// </summary>
        public static Color GetSelectionYellow()
        {
            return IsDarkTheme() 
                ? new Color(1f, 0.9f, 0.3f, 1f)  // Bright yellow for dark theme
                : new Color(0.9f, 0.8f, 0.2f, 1f); // Darker yellow for light theme
        }

        /// <summary>
        /// Gets theme-aware hover color
        /// </summary>
        public static Color GetHoverColor()
        {
            return IsDarkTheme()
                ? new Color(0.3f, 0.3f, 0.3f, 1f)  // Light gray for dark theme
                : new Color(0.9f, 0.9f, 0.9f, 1f); // Dark gray for light theme
        }

        /// <summary>
        /// Normal button style for type list
        /// </summary>
        public static GUIStyle NormalButtonStyle()
        {
            if (_normalButtonStyle == null)
            {
                _normalButtonStyle = new GUIStyle(EditorStyles.label)
                {
                    padding = Spacing.BUTTON_PADDING,
                    margin = Spacing.BUTTON_MARGIN,
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 12
                };

                // Theme-aware text colors
                var textColor = IsDarkTheme() ? Color.white : Color.black;
                _normalButtonStyle.normal.textColor = textColor;
                _normalButtonStyle.hover.textColor = IsDarkTheme() ? Color.cyan : new Color(0.2f, 0.4f, 0.8f);
                _normalButtonStyle.active.textColor = _normalButtonStyle.hover.textColor;

                // Hover background
                _normalButtonStyle.hover.background = GetSolidColorTex(GetHoverColor());
            }
            return _normalButtonStyle;
        }

        /// <summary>
        /// Selected button style with yellow background
        /// </summary>
        public static GUIStyle SelectedButtonStyle()
        {
            if (_selectedButtonStyle == null)
            {
                _selectedButtonStyle = new GUIStyle(NormalButtonStyle());

                var selectionColor = GetSelectionYellow();
                
                // Yellow background for selected state
                _selectedButtonStyle.normal.background = GetSolidColorTex(selectionColor);
                _selectedButtonStyle.hover.background = GetSolidColorTex(Color.Lerp(selectionColor, Color.white, 0.1f));
                _selectedButtonStyle.active.background = GetSolidColorTex(Color.Lerp(selectionColor, Color.black, 0.1f));

                // Dark text for contrast on yellow background
                var textColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                _selectedButtonStyle.normal.textColor = textColor;
                _selectedButtonStyle.hover.textColor = textColor;
                _selectedButtonStyle.active.textColor = textColor;
                
                _selectedButtonStyle.fontStyle = FontStyle.Bold;
            }
            return _selectedButtonStyle;
        }

        /// <summary>
        /// Creates or gets cached solid color texture
        /// </summary>
        private static Texture2D GetSolidColorTex(Color color)
        {
            if (_textureCache.TryGetValue(color, out Texture2D tex))
            {
                return tex;
            }

            tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            _textureCache[color] = tex;
            return tex;
        }

        /// <summary>
        /// Draws a subtle underline beneath the last GUI element
        /// </summary>
        public static void DrawUnderline(Color? color = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            
            Rect r = GUILayoutUtility.GetLastRect();
            var lineColor = color ?? (IsDarkTheme() ? Color.gray : Color.black);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax, r.width, 1), lineColor);
        }

        /// <summary>
        /// Draws a vertical separator line
        /// </summary>
        public static void DrawVerticalLine(
            float width = 1f,
            float height = 20f,
            float xOffset = 2f,
            float yOffset = 0f,
            Color? color = null)
        {
            if (Event.current.type != EventType.Repaint) return;

            Rect r = GUILayoutUtility.GetLastRect();
            Rect line = new Rect(
                r.xMax + xOffset,
                r.y + yOffset,
                width,
                height);

            var lineColor = color ?? (IsDarkTheme() ? Color.gray : Color.black);
            EditorGUI.DrawRect(line, lineColor);
        }

        /// <summary>
        /// Creates a professional header style
        /// </summary>
        public static GUIStyle HeaderStyle()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 8, 8)
            };
            
            headerStyle.normal.textColor = IsDarkTheme() ? 
                new Color(0.9f, 0.9f, 0.9f) : 
                new Color(0.2f, 0.2f, 0.2f);
                
            return headerStyle;
        }

        /// <summary>
        /// Cleanup cached textures when editor closes
        /// </summary>
        public static void CleanupCache()
        {
            foreach (var texture in _textureCache.Values)
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }
            _textureCache.Clear();
            
            // Reset cached styles to force recreation
            _normalButtonStyle = null;
            _selectedButtonStyle = null;
            _hoverButtonStyle = null;
        }
    }
}
