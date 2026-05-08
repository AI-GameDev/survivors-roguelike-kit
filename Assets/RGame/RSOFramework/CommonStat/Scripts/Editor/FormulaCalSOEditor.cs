#region

using System;
using System.Collections.Generic;
using System.Linq;
using RGame.Framework;
using UnityEditor;
using UnityEngine;

#endregion

namespace RGame.CommonStat.Editor
{
    /// <summary>
    ///     Custom editor for FormulaCalSO to preview formula calculation results
    /// </summary>
    [CustomEditor(typeof(FormulaCalSO))]
    public class FormulaCalSOEditor : DescriptionBaseSOEditor
    {
        private string calculationResult = "";
        private bool isValid = true;
        private List<FormulaParameter> parameters;
        private CommonStatRuntimeSO runtimeValues;
        private bool showParameters = true;

        /// <summary>
        ///     Initialize editor state when the object is loaded
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            parameters = new List<FormulaParameter>();
        }

        /// <summary>
        ///     Draws the custom inspector GUI
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(15);

            var formulaCal = (FormulaCalSO)target;

            // Runtime Values field
            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 18;
            EditorGUILayout.LabelField("Preview Settings", titleStyle);
            
            EditorGUILayout.Space(10);
            
            runtimeValues = (CommonStatRuntimeSO)EditorGUILayout.ObjectField(
                "Runtime Values",
                runtimeValues,
                typeof(CommonStatRuntimeSO),
                false
            );

            // Parameters section
            EditorGUILayout.Space(5);
            showParameters = EditorGUILayout.Foldout(showParameters, "Parameters");
            if (showParameters)
            {
                EditorGUI.indentLevel++;

                // Add new parameter button
                if (GUILayout.Button("Add Parameter", GUILayout.Width(120)))
                {
                    Undo.RecordObject(this, "Add Formula Parameter");
                    parameters.Add(new FormulaParameter());
                    EditorUtility.SetDirty(this);
                }

                EditorGUILayout.Space(5);

                // Parameters list
                for (var i = 0; i < parameters.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Delete button
                    if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(20)))
                    {
                        Undo.RecordObject(this, "Remove Formula Parameter");
                        parameters.RemoveAt(i);
                        EditorUtility.SetDirty(this);
                        i--; // Adjust index after removal
                        GUIUtility.ExitGUI(); // Prevent GUI errors
                        continue;
                    }

                    // Parameter fields
                    EditorGUILayout.BeginVertical();

                    EditorGUI.BeginChangeCheck();
                    var newKey = EditorGUILayout.TextField("Key", parameters[i].Key);
                    var newValue = EditorGUILayout.DoubleField("Value", parameters[i].Value);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this, "Edit Formula Parameter");
                        parameters[i].Key = newKey;
                        parameters[i].Value = newValue;
                        EditorUtility.SetDirty(this);
                    }

                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    if (i < parameters.Count - 1) EditorGUILayout.Space(2);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Calculate button
            if (GUILayout.Button("Calculate")) CalculateResult(formulaCal);

            // Display result
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel, GUILayout.Width(50));
            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = isValid ? Color.green : Color.red;
            EditorGUILayout.LabelField(calculationResult, style);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var styleTip = new GUIStyle(EditorStyles.boldLabel);
            styleTip.fontSize = 14;
            styleTip.alignment = TextAnchor.MiddleCenter;
            styleTip.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            EditorGUILayout.LabelField(
                "Used only in Editor, no storage function provided.",
                styleTip,
                GUILayout.Width(500)
            );

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            
            DrawSceneReferences();
        }

        /// <summary>
        ///     Calculates the formula result with current settings
        /// </summary>
        private void CalculateResult(FormulaCalSO formulaCal)
        {
            if (string.IsNullOrEmpty(formulaCal.Formula))
            {
                isValid = false;
                calculationResult = "Error: Formula is empty";
                return;
            }

            if (runtimeValues == null)
            {
                isValid = false;
                calculationResult = "Error: Runtime Values not set";
                return;
            }

            try
            {
                var parametersDictionary = parameters
                    .Where(p => !string.IsNullOrEmpty(p.Key))
                    .ToDictionary(p => p.Key, p => p.Value);

                var result = formulaCal.Evaluate(runtimeValues, parametersDictionary);
                isValid = true;
                calculationResult = result.ToString();
            }
            catch (Exception e)
            {
                isValid = false;
                calculationResult = $"Error: Invalid formula - {e.Message}";
            }
        }
    }
}