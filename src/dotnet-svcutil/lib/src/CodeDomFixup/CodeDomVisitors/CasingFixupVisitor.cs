// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CodeDom; // <-- ADDED: Includes CodeCompileUnit, MemberAttributes, etc.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeDom;

namespace Microsoft.Tools.ServiceModel.Svcutil.CodeDomFixup.CodeDomVisitors
{
    // A CodeDomVisitor that applies a PascalCase to camelCase conversion
    // for public properties and their backing fields in generated DataContract types.
    internal class CasingFixupVisitor : CodeDomVisitor
    {
        private const string MessageContractAttributeName = "MessageContractAttribute";

        // Assuming the base class uses these signatures (VisitCodeCompileUnit is the entry point)
        protected override void Visit(CodeCompileUnit codeCompileUnit)
        {
            base.Visit(codeCompileUnit);
        }

        // Renames PascalCase to camelCase (e.g., MyProperty -> myProperty)
        private string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 2)
            {
                return name;
            }

            return char.ToLower(name[0], CultureInfo.InvariantCulture) + name.Substring(1);
        }

        protected override void Visit(CodeMemberProperty codeMemberProperty)
        {
            // Only convert public properties
            if ((codeMemberProperty.Attributes & MemberAttributes.Public) == MemberAttributes.Public)
            {
                // Do not change property names in MessageContract types as they map directly to XML/SOAP elements.
                if (!IsMessageContractType(codeMemberProperty))
                {
                    codeMemberProperty.Name = ToCamelCase(codeMemberProperty.Name);
                }
            }

            base.Visit(codeMemberProperty);
        }

        protected override void Visit(CodeMemberField codeMemberField)
        {
            // Only convert private fields (likely backing fields)
            if ((codeMemberField.Attributes & MemberAttributes.AccessMask) == MemberAttributes.Private)
            {
                // Strip leading underscore if present and convert to camelCase.
                string fieldName = codeMemberField.Name.TrimStart('_');
                codeMemberField.Name = ToCamelCase(fieldName);
            }

            base.Visit(codeMemberField);
        }

        // Helper to check if the property's containing class is a MessageContract.
        private bool IsMessageContractType(CodeMemberProperty codeMemberProperty)
        {
            CodeTypeDeclaration declaringType = codeMemberProperty.UserData[typeof(CodeTypeDeclaration)] as CodeTypeDeclaration;
            if (declaringType != null)
            {
                return declaringType.CustomAttributes.Cast<CodeAttributeDeclaration>()
                    .Any(attr => attr.AttributeType.BaseType == MessageContractAttributeName);
            }
            return false;
        }
    }
}
