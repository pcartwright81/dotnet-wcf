// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeDom;

// Assuming Microsoft.Tools.ServiceModel.Svcutil namespace contains CodeDomVisitor
namespace Microsoft.Tools.ServiceModel.Svcutil.CodeDomFixup.CodeDomVisitors
{
    // A CodeDomVisitor that applies a PascalCase to camelCase conversion
    // for public properties and their backing fields in generated DataContract types.
    internal class CasingFixupVisitor : CodeDomVisitor
    {
        private const string MessageContractAttributeName = "MessageContractAttribute";
        private const string DataMemberAttributeName = "DataMemberAttribute"; // <-- ADDED

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
                // Do not change property names in MessageContract types (they map directly to XML/SOAP elements).
                if (!IsMessageContractType(codeMemberProperty))
                {
                    string originalName = codeMemberProperty.Name;
                    string camelCaseName = ToCamelCase(originalName);

                    if (originalName != camelCaseName)
                    {
                        // 1. Rename the C# property
                        codeMemberProperty.Name = camelCaseName;

                        // 2. Check for and update DataMemberAttribute
                        var dataMemberAttribute = codeMemberProperty.CustomAttributes.Cast<CodeAttributeDeclaration>()
                            .FirstOrDefault(attr => attr.AttributeType.BaseType.EndsWith(DataMemberAttributeName));

                        if (dataMemberAttribute != null)
                        {
                            // DataMemberAttribute controls the name on the wire. We must ensure it uses the camelCase name.

                            var nameArgument = dataMemberAttribute.Arguments.Cast<CodeAttributeArgument>()
                                .FirstOrDefault(arg => arg.Name == "Name");

                            if (nameArgument == null)
                            {
                                // No 'Name' argument exists (it currently defaults to the PascalCase C# name).
                                // Add a new 'Name' argument with the camelCase value.
                                dataMemberAttribute.Arguments.Add(
                                    new CodeAttributeArgument("Name", new CodePrimitiveExpression(camelCaseName)));
                            }
                            else
                            {
                                // An existing 'Name' argument needs its value updated.
                                nameArgument.Value = new CodePrimitiveExpression(camelCaseName);
                            }
                        }
                    }
                }
            }

            base.Visit(codeMemberProperty);
        }

        protected override void Visit(CodeMemberField codeMemberField)
        {
            // Logic for fields (typically backing fields) remains correct, 
            // as they do not have a DataMemberAttribute to update.
            if ((codeMemberField.Attributes & MemberAttributes.AccessMask) == MemberAttributes.Private)
            {
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
