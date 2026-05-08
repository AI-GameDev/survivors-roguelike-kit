using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace RGame.CommonStat.Example
{
    /// <summary>
    /// UI component for formula input and result display
    /// </summary>
    public class InputFormalUI : MonoBehaviour
    {
        [Header("References")] public CommonStatRuntimeSO ValueSO;

        [SerializeField] private TMP_InputField formulaInput;

        [SerializeField] private TextMeshProUGUI resultText;

        [Header("Settings")] [SerializeField] private Color validResultColor = Color.green;

        [SerializeField] private Color errorResultColor = Color.red;

        private FormulaCalSO tempFormulaCal;

        /// <summary>
        /// Initialize the component
        /// </summary>
        private void Awake()
        {
            // Create temporary FormulaCalSO instance
            tempFormulaCal = ScriptableObject.CreateInstance<FormulaCalSO>();

            // Setup input field listener
            if (formulaInput != null)
            {
                OnFormulaInputChanged(formulaInput.text);
            }
            else
            {
                Debug.LogError("Formula Input field is not assigned!");
            }

            if (resultText == null)
            {
                Debug.LogError("Result Text is not assigned!");
            }
        }

        private void Update()
        {
            OnFormulaInputChanged(formulaInput.text);
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        private void OnDestroy()
        {
            if (tempFormulaCal != null)
            {
                Destroy(tempFormulaCal);
            }
        }

        /// <summary>
        /// Handle formula input changes
        /// </summary>
        private void OnFormulaInputChanged(string newFormula)
        {
            if (ValueSO == null)
            {
                DisplayError("ValueSO is not assigned!");
                return;
            }

            if (string.IsNullOrEmpty(newFormula))
            {
                DisplayError("Formula is empty");
                return;
            }

            EvaluateFormula(newFormula);
        }

        /// <summary>
        /// Evaluate the formula and display the result
        /// </summary>
        private void EvaluateFormula(string formula)
        {
            try
            {
                tempFormulaCal.Formula = formula;
                int result = tempFormulaCal.Evaluate(ValueSO);
                DisplayResult(result.ToString());
            }
            catch (System.Exception e)
            {
                DisplayError($"Invalid formula: {e.Message}");
            }
        }

        /// <summary>
        /// Display the calculation result
        /// </summary>
        private void DisplayResult(string result)
        {
            if (resultText != null)
            {
                resultText.text = result;
                resultText.color = validResultColor;
            }
        }

        /// <summary>
        /// Display an error message
        /// </summary>
        private void DisplayError(string error)
        {
            if (resultText != null)
            {
                resultText.text = error;
                resultText.color = errorResultColor;
            }
        }
    }
}