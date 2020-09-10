﻿using System.Collections.Generic;
using System.Linq;
using DynamicExpresso;
using NetCasbin.Abstractions;
using NetCasbin.Model;
using NetCasbin.Util;

namespace NetCasbin.Evaluation
{
    public class ExpressionProvider : IExpressionProvider
    {
        private readonly IDictionary<string, Lambda> _expressionCache
            = new Dictionary<string, Lambda>();
        private readonly FunctionMap _functionMap = FunctionMap.LoadFunctionMap();
        private readonly Model.Model _model;
        private Interpreter _interpreter;

        public ExpressionProvider(Model.Model model,
            string requestType = PermConstants.DefaultRequestType,
            string policyType = PermConstants.DefaultPolicyType)
        {
            _model = model;
            if (model.Model.ContainsKey(PermConstants.Section.RequestSection))
            {
                RequestAssertion = model.Model[PermConstants.Section.RequestSection][requestType];
            }
            if (model.Model.ContainsKey(PermConstants.Section.PolicySection))
            {
                PolicyAssertion = model.Model[PermConstants.Section.PolicySection][policyType];
            }
        }

        public Assertion RequestAssertion { get; }
        public Assertion PolicyAssertion { get; }

        private IDictionary<string, int> RequestTokens => RequestAssertion.Tokens;
        private IDictionary<string, int> PolicyTokens => PolicyAssertion.Tokens;

        public void SetFunction(string name, AbstractFunction function)
        {
            var interpreter = GetInterpreter();
            interpreter.SetFunction(name, function);
        }

        public Lambda GetExpression(string expressionString, IReadOnlyList<object> requestValues)
        {
            if (_expressionCache.ContainsKey(expressionString))
            {
                return _expressionCache[expressionString];
            }

            Lambda expression = CreateExpression(expressionString, requestValues);
            _expressionCache[expressionString] = expression;
            return expression;
        }

        private Lambda CreateExpression(string expressionString, IReadOnlyList<object> requestValues)
        {
            Parameter[] parameterArray = GetParameters(requestValues).Values.ToArray();
            Interpreter interpreter = GetInterpreter();
            return interpreter.Parse(expressionString, parameterArray);;
        }

        public IDictionary<string, Parameter> GetParameters(
            IReadOnlyList<object> requestValues = null,
            IReadOnlyList<string> policyValues = null)
        {
            var parameters = new Dictionary<string, Parameter>();
            foreach (string token in RequestTokens.Keys)
            {
                object requestValue = requestValues?[RequestTokens[token]] ?? string.Empty;
                parameters.Add(token, new Parameter(token, requestValue));
            }
            foreach (string token in PolicyTokens.Keys)
            {
                string policyValue = policyValues?[PolicyTokens[token]] ?? string.Empty;
                parameters.Add(token, new Parameter(token, policyValue));
            }
            return parameters;
        }

        private Interpreter GetInterpreter()
        {
            if (_interpreter is not null)
            {
                return _interpreter;
            }

            _interpreter = CreateInterpreter();
            return _interpreter;
        }

        private Interpreter CreateInterpreter()
        {
            var interpreter = new Interpreter();
            foreach (KeyValuePair<string, AbstractFunction> functionKeyValue in _functionMap.FunctionDict)
            {
                interpreter.SetFunction(functionKeyValue.Key, functionKeyValue.Value);
            }

            if (!_model.Model.ContainsKey(PermConstants.Section.RoleSection))
            {
                return interpreter;
            }

            foreach (KeyValuePair<string, Assertion> assertionKeyValue in _model.Model[PermConstants.Section.RoleSection])
            {
                string key = assertionKeyValue.Key;
                Assertion assertion = assertionKeyValue.Value;
                interpreter.SetFunction(key, BuiltInFunctions.GenerateGFunction(key, assertion.RoleManager));
            }
            return interpreter;
        }
    }
}