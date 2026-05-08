#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = System.Random;

#endregion

namespace RGame.CommonStat
{
    public static class FormulaParser
    {
        private static readonly Random RandomInstance = new();

        public static int Evaluate(string formula, Dictionary<string, (int, int)> variables, Dictionary<string, double> parameters = null)
        {
            try
            {
                var tokens = Tokenize(formula);

                tokens = ReplaceVariables(tokens, variables, parameters ?? new Dictionary<string, double>());

                var result = EvaluateExpression(tokens);

                // Check if formula starts with a variable token and result is negative
                if (tokens.Count > 0 &&
                    tokens[0].Type == TokenType.Variable &&
                    result < 0)
                    return 0;

                var roundedResult = (int)Math.Round(result);
                return roundedResult;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error evaluating formula: {formula}");
                Debug.LogError(ex);
                throw;
            }
        }

        private static List<Token> Tokenize(string formula)
        {
            var tokens = new List<Token>();
            var pattern = @"(\${[a-zA-Z_][a-zA-Z0-9_]*})|" + // Variables
                          @"(@{[a-zA-Z_][a-zA-Z0-9_]*})|" + // Parameters
                          @"([\+\-\*/%])|" + // Operators
                          @"([a-zA-Z_][a-zA-Z0-9_]*)|" + // Function names
                          @"(\d*\.?\d+)|" + // Numbers
                          @"(\(|\))|" + // Parentheses
                          @"(,)"; // Comma

            var matches = Regex.Matches(formula, pattern);
            var expectOperand = true;

            for (var i = 0; i < matches.Count; i++)
            {
                var value = matches[i].Value;
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (Regex.IsMatch(value, @"^\${[a-zA-Z_][a-zA-Z0-9_]*}$"))
                {
                    tokens.Add(new Token(TokenType.Variable, value.Trim('$', '{', '}')));
                    expectOperand = false;
                }

                if (Regex.IsMatch(value, @"^@{[a-zA-Z_][a-zA-Z0-9_]*}$"))
                {
                    tokens.Add(new Token(TokenType.Parameter, value.Trim('@', '{', '}')));
                    expectOperand = false;
                }

                else if (Regex.IsMatch(value, @"^[\+\-\*/%]$"))
                {
                    if (value == "-" && expectOperand)
                        if (i + 1 < matches.Count)
                        {
                            var nextMatch = matches[i + 1].Value;
                            if (Regex.IsMatch(nextMatch, @"^\d*\.?\d+$"))
                            {
                                tokens.Add(new Token(TokenType.Number, "-" + nextMatch));
                                i++; // 跳过下一个token
                                expectOperand = false;
                                continue;
                            }
                        }

                    tokens.Add(new Token(TokenType.Operator, value));
                    expectOperand = true;
                }
                else if (Regex.IsMatch(value, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                {
                    tokens.Add(new Token(TokenType.Function, value.ToLower()));
                    expectOperand = true;
                }
                else if (Regex.IsMatch(value, @"^\d*\.?\d+$"))
                {
                    tokens.Add(new Token(TokenType.Number, value));
                    expectOperand = false;
                }
                else if (value == "(")
                {
                    tokens.Add(new Token(TokenType.LeftParen, value));
                    expectOperand = true;
                }
                else if (value == ")")
                {
                    tokens.Add(new Token(TokenType.RightParen, value));
                    expectOperand = false;
                }
                else if (value == ",")
                {
                    tokens.Add(new Token(TokenType.Comma, value));
                    expectOperand = true;
                }
            }

            return tokens;
        }

        private static List<Token> ReplaceVariables(
            List<Token> tokens,
            Dictionary<string, (int Current, int Max)> variables,
            Dictionary<string, double> parameters)
        {
            return tokens.Select(token =>
            {
                if (token.Type == TokenType.Variable)
                {
                    // 原有的变量处理逻辑保持不变
                    var varName = token.Value;
                    var isMax = false;

                    if (varName.EndsWith("Max", StringComparison.OrdinalIgnoreCase))
                    {
                        isMax = true;
                        varName = varName.Substring(0, varName.Length - 3);
                    }

                    if (variables.TryGetValue(varName, out var value))
                    {
                        var valueToUse = isMax ? value.Max : value.Current;
                        return new Token(TokenType.Number, valueToUse.ToString());
                    }

                    throw new Exception($"Variable '${{{token.Value}}}' not found.");
                }

                if (token.Type == TokenType.Parameter)
                {
                    // 新增：参数处理逻辑
                    if (parameters != null && parameters.TryGetValue(token.Value, out var paramValue)) return new Token(TokenType.Number, paramValue.ToString());

                    throw new Exception($"Parameter '@{{{token.Value}}}' not found.");
                }

                return token;
            }).ToList();
        }

        private static double EvaluateExpression(List<Token> tokens)
        {
            var values = new Stack<double>();
            var operators = new Stack<Token>();

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                switch (token.Type)
                {
                    case TokenType.Number:
                        values.Push(double.Parse(token.Value));
                        break;

                    case TokenType.Function:
                        operators.Push(token);
                        break;

                    case TokenType.LeftParen:
                        operators.Push(token);
                        break;

                    case TokenType.RightParen:
                        while (operators.Count > 0 && operators.Peek().Type != TokenType.LeftParen) EvaluateTop(values, operators);

                        if (operators.Count > 0)
                        {
                            operators.Pop(); // Remove left parenthesis
                            if (operators.Count > 0 && operators.Peek().Type == TokenType.Function) EvaluateTop(values, operators);
                        }

                        break;

                    case TokenType.Operator:
                        while (operators.Count > 0 && ShouldEvaluate(operators.Peek(), token)) EvaluateTop(values, operators);

                        operators.Push(token);
                        break;

                    case TokenType.Comma:
                        while (operators.Count > 0 && operators.Peek().Type != TokenType.LeftParen) EvaluateTop(values, operators);

                        break;
                }
            }

            while (operators.Count > 0) EvaluateTop(values, operators);

            return values.Pop();
        }

        private static void EvaluateTop(Stack<double> values, Stack<Token> operators)
        {
            var op = operators.Pop();
            if (op.Type == TokenType.Function)
            {
                var args = new List<double>();
                if (values.Count > 0)
                {
                    args.Insert(0, values.Pop());
                    if ((op.Value == "pow" || op.Value == "random" || op.Value == "min" ||
                         op.Value == "max" || op.Value == "dice") && values.Count > 0)
                        args.Insert(0, values.Pop());
                }

                values.Push(ApplyFunction(op.Value, args.ToArray()));
            }
            else if (op.Type == TokenType.Operator)
            {
                if (values.Count < 2)
                    throw new InvalidOperationException("Invalid expression: not enough operands");
                var b = values.Pop();
                var a = values.Pop();
                values.Push(ApplyOperator(a, b, op.Value));
            }
        }

        private static bool ShouldEvaluate(Token op1, Token op2)
        {
            if (op1.Type == TokenType.LeftParen || op1.Type == TokenType.Function)
                return false;

            if (op1.Type == TokenType.Operator && op2.Type == TokenType.Operator)
            {
                var p1 = GetPrecedence(op1.Value);
                var p2 = GetPrecedence(op2.Value);
                return p1 >= p2;
            }

            return false;
        }

        private static int GetPrecedence(string op)
        {
            switch (op)
            {
                case "*":
                case "/":
                case "%":
                    return 2;
                case "+":
                case "-":
                    return 1;
                default:
                    return 0;
            }
        }

        private static double ApplyOperator(double a, double b, string op)
        {
            switch (op)
            {
                case "+":
                    return a + b;
                case "-":
                    return a - b;
                case "*":
                    return a * b;
                case "/":
                    if (b == 0) throw new DivideByZeroException();
                    return a / b;
                case "%":
                    if (b == 0) throw new DivideByZeroException();
                    return a % b;
                default:
                    throw new ArgumentException($"Unknown operator: {op}");
            }
        }

        private static double ApplyFunction(string name, double[] args)
        {
            if (args.Length == 0)
                throw new ArgumentException($"No arguments provided for function: {name}");

            switch (name)
            {
                case "pow":
                    if (args.Length != 2) throw new ArgumentException("pow requires 2 arguments");
                    return Math.Pow(args[0], args[1]);
                case "abs":
                    return Math.Abs(args[0]);
                case "sqrt":
                    return Math.Sqrt(args[0]);
                case "sin":
                    return Math.Sin(args[0] * Math.PI / 180);
                case "cos":
                    return Math.Cos(args[0] * Math.PI / 180);
                case "random":
                    if (args.Length != 2) throw new ArgumentException("random requires 2 arguments");
                    return RandomInstance.Next((int)args[0], (int)args[1] + 1);
                case "min":
                    if (args.Length != 2) throw new ArgumentException("min requires 2 arguments");
                    return Math.Min(args[0], args[1]);
                case "max":
                    if (args.Length != 2) throw new ArgumentException("max requires 2 arguments");
                    return Math.Max(args[0], args[1]);
                case "dice":
                    if (args.Length != 2) throw new ArgumentException("dice requires 2 arguments");
                    return Enumerable.Range(0, (int)args[0]).Sum(_ => RandomInstance.Next(1, (int)args[1] + 1));
                case "chance":
                    return RandomInstance.NextDouble() * 100 < args[0] ? 1.0 : 0.0;
                default:
                    throw new ArgumentException($"Unknown function: {name}");
            }
        }

        // Token types for lexical analysis
        private enum TokenType
        {
            Number,
            Operator,
            Function,
            Variable,
            Parameter,
            LeftParen,
            RightParen,
            Comma
        }

        // Token class for lexical elements
        private class Token
        {
            public Token(TokenType type, string value)
            {
                Type = type;
                Value = value;
            }

            public TokenType Type { get; }
            public string Value { get; }

            public override string ToString()
            {
                return $"{Type}:{Value}";
            }
        }
    }
}