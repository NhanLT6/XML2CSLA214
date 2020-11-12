﻿Imports pbs.Helper.Extensions
Imports pbs.Helper
Imports System.Text.RegularExpressions
Imports pbs.BO

Namespace MetaData

    Partial Public Class ClassField

        Friend ClassName As String = ""

        Function BuildProperty() As String
            Return String.Join(Environment.NewLine, GetSingleProperty_Editable().ToArray)
        End Function

        Function BuildReadonlyProperty() As String
            Return String.Join(Environment.NewLine, GetSingleProperty_ReadOnly().ToArray)
        End Function

        Function BuildChildProperty() As String
            Dim ret = New List(Of String)

            Dim thePropType = PropType.Leaf

            Dim thePropName = FieldName

            ret.Add($"public static readonly PropertyInfo<{thePropType}> {FieldName}Property = RegisterProperty<{thePropType}>(c => c.{FieldName}, RelationshipTypes.Child | RelationshipTypes.LazyLoad);")

            ret.Add(<prop>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CSLA0007: Evaluate Properties for Simplicity", Justification = "")]
        [TableRangeInfo("<%= GroupName %>")]
        public <%= thePropType %><%= " " %><%= thePropName %>
        {
            get
            {
                if (!FieldManager.FieldExists(<%= thePropName %>Property))
                {
                    <%= thePropName %> = <%= thePropType %>.NewChild<%= thePropType %>(this);
                }
                return GetProperty(<%= thePropName %>Property);
            }
            private set
            {
                LoadProperty(<%= thePropName %>Property, value);
                OnPropertyChanged(<%= thePropName %>Property);
            }
        }</prop>.Value)

            ret.Add(Environment.NewLine)

            Return String.Join(Environment.NewLine, ret.ToArray)

        End Function

        Function BuildChildFetching(parentKey As String) As String
            Dim ret = New List(Of String)

            Dim thePropType = PropType.Leaf

            Dim thePropName = FieldName

            Dim ChildDBTable = ""

            Dim theKidType = pbs.BO.pbsAssemblies.GetClassType(ChildType)
            If theKidType IsNot Nothing Then
                For Each attr As DBAttribute In theKidType.GetCustomAttributes(GetType(DBAttribute), True)
                    ChildDBTable = attr.TableName
                    Exit For
                Next
            End If

            ret.Add(<prop> using (var cm = cn.CreateSQLCommand())
            {
                cm.SetDBCommand(CommandType.Text, $"SELECT * FROM <%= ChildDBTable %>{DTB} WHERE ORDER_NO = '{criteria.<%= parentKey %>}'");

                using (SafeDataReader dr = new SafeDataReader(cm.ExecuteReader()))
                {
                    <%= thePropName %> = <%= thePropType %>.Get<%= thePropType %>(dr,this);
                }

            } </prop>.Value)

            ret.Add(Environment.NewLine)

            Return String.Join(Environment.NewLine, ret.ToArray)

        End Function

        Function BuildChildInsertUpdate() As String

            Dim thePropName = FieldName

            Return <prop><%= FieldName %>.Update(cn,this); </prop>.Value

        End Function


        Function DeleteChildTableScript(parentKey As String) As String

            Dim ChildDBTable = ""

            Dim theKidType = pbs.BO.pbsAssemblies.GetClassType(ChildType)
            If theKidType IsNot Nothing Then
                For Each attr As DBAttribute In theKidType.GetCustomAttributes(GetType(DBAttribute), True)
                    ChildDBTable = attr.TableName
                    Exit For
                Next
            End If


            Return <prop>cm.CommandText += $"DELETE FROM <%= ChildDBTable %>{DTB} WHERE HEADER_NO= {criteria.<%= parentKey %>}";</prop>.Value

        End Function

        Private Function GetSingleProperty_Editable() As List(Of String)
            Dim ret = New List(Of String)

            Dim theFieldType = Mapper.FieldType411(FieldType)

            Dim thePropType = PropType.ToLower

            Dim thePropName = FieldName

            Dim theDefault = DefaultValue?.Replace("pbs.Helper.Smart", "Smart")

            If theDefault Is Nothing Then
                ret.Add($"public static readonly PropertyInfo<{theFieldType}> {thePropName}Property = RegisterProperty<{theFieldType}>(c => c.{thePropName});")
            Else
                ret.Add($"public static readonly PropertyInfo<{theFieldType}> {thePropName}Property = RegisterProperty<{theFieldType}>(c => c.{thePropName},{theDefault});")
            End If

            If IsPrimaryKey Then ret.Add($"[System.ComponentModel.DataObjectField(true,{IsAutoGenerated.ToString.ToLower})]")

            Dim theCellInfo = GetCellInfoAttribute()
            If theCellInfo IsNot Nothing Then ret.Add(theCellInfo)

            Dim theRuleAttribute = GetRuleAttribute()
            If theRuleAttribute IsNot Nothing Then ret.Add(theRuleAttribute)

            Dim theDBAttribute = GetDBAttribute()
            If theDBAttribute IsNot Nothing Then ret.Add(theDBAttribute)

            ret.Add($"public {thePropType} {thePropName}")
            ret.Add("{")

            If thePropType = theFieldType Then
                ret.Add($" get {{ return GetProperty({thePropName}Property); }}")

            ElseIf thePropType.MatchesRegExp("bool") AndAlso theFieldType.MatchesRegExp("string|char") Then
                ret.Add($" get {{ return GetProperty({thePropName}Property).ToBoolean(); }}")

            Else
                ret.Add($" get {{ return GetPropertyConvert<{theFieldType},{thePropType}>({thePropName}Property); }}")
            End If

            If IsPrimaryKey Then
                If thePropType <> theFieldType Then
                    ret.Add($"private set {{ LoadPropertyConvert<{theFieldType},{thePropType}>({thePropName}Property, value); }}")
                Else
                    ret.Add($"private set {{ LoadProperty({thePropName}Property, value); }}")
                End If

            Else
                If IsReadonly() Then
                    If thePropType <> theFieldType AndAlso thePropType.MatchesRegExp("bool") AndAlso theFieldType.MatchesRegExp("string|char") Then
                        ret.Add($"private set {{ LoadProperty({thePropName}Property, value?""Y"":""N""); }}")

                    ElseIf thePropType <> theFieldType Then
                        ret.Add($"private set {{ LoadPropertyConvert<{theFieldType},{thePropType}>({thePropName}Property, value); }}")
                    Else
                        ret.Add($"private set {{ LoadProperty({thePropName}Property, value); }}")
                    End If
                Else
                    If thePropType <> theFieldType AndAlso thePropType.MatchesRegExp("bool") AndAlso theFieldType.MatchesRegExp("string|char") Then
                        ret.Add($" set {{ SetProperty({thePropName}Property, value?""Y"":""N""); }}")

                    ElseIf thePropType <> theFieldType Then
                        ret.Add($" set {{ SetPropertyConvert<{theFieldType},{thePropType}>({thePropName}Property, value); }}")
                    Else
                        ret.Add($" set {{ SetProperty({thePropName}Property, value); }}")
                    End If
                End If

            End If

            ret.Add("}")

            ret.Add(Environment.NewLine)

            Return ret

        End Function

        Private Function GetSingleProperty_ReadOnly() As List(Of String)
            Dim ret = New List(Of String)

            Dim theFieldType = Mapper.FieldType411(FieldType)

            Dim thePropType = PropType.ToLower

            Dim thePropName = FieldName

            Dim theDefault = DefaultValue?.Replace("pbs.Helper.Smart", "Smart")

            If theDefault Is Nothing Then
                ret.Add($"public static readonly PropertyInfo<{theFieldType}> {thePropName}Property = RegisterProperty<{theFieldType}>(c => c.{thePropName});")
            Else
                ret.Add($"public static readonly PropertyInfo<{theFieldType}> {thePropName}Property = RegisterProperty<{theFieldType}>(c => c.{thePropName},{theDefault});")
            End If

            If IsPrimaryKey Then ret.Add($"[System.ComponentModel.DataObjectField(true,{IsAutoGenerated.ToString.ToLower})]")

            ret.Add($"public {thePropType} {thePropName}")
            ret.Add("{")

            If thePropType = theFieldType Then
                ret.Add($" get {{ return ReadProperty({thePropName}Property); }}")

            ElseIf thePropType.MatchesRegExp("bool") AndAlso theFieldType.MatchesRegExp("string|char") Then
                ret.Add($" get {{ return ReadProperty({thePropName}Property).ToBoolean(); }}")

            Else
                ret.Add($" get {{ return ReadPropertyConvert<{theFieldType},{thePropType}>({thePropName}Property); }}")
            End If

            If IsPrimaryKey Then
                If thePropType <> theFieldType Then
                    ret.Add($"private set {{ LoadPropertyConvert<{theFieldType},{thePropType}>({thePropName}Property, value); }}")
                Else
                    ret.Add($"private set {{ LoadProperty({thePropName}Property, value); }}")
                End If

            Else

                If thePropType <> theFieldType AndAlso thePropType.MatchesRegExp("bool") AndAlso theFieldType.MatchesRegExp("string|char") Then
                    ret.Add($"private set {{ LoadProperty({thePropName}Property, value?""Y"":""N""); }}")

                ElseIf thePropType <> theFieldType Then
                    ret.Add($"private set {{ LoadPropertyConvert<{theFieldType},{thePropType}>({thePropName}Property, value); }}")
                Else
                    ret.Add($"private set {{ LoadProperty({thePropName}Property, value); }}")
                End If

            End If

            ret.Add("}")

            ret.Add(Environment.NewLine)

            Return ret

        End Function

        Function BuildCriteriaProperty() As List(Of String)

            Dim ret = New List(Of String)

            Dim theFieldType = Mapper.FieldType411(FieldType)

            Dim thePropType = PropType
            Dim thePropName = FieldName

            Dim theDefault = DefaultValue?.Replace("pbs.Helper.Smart", "Smart")

            If theDefault Is Nothing Then
                ret.Add($"public static readonly PropertyInfo<{theFieldType}> {thePropName}Property = RegisterProperty<{theFieldType}>(c => c.{thePropName});")
            Else
                ret.Add($"public static readonly PropertyInfo<{theFieldType}> {thePropName}Property = RegisterProperty<{theFieldType}>(c => c.{thePropName},{theDefault});")
            End If


            ret.Add($"public {thePropType} {thePropName}")
            ret.Add("{")

            If thePropType = theFieldType Then
                ret.Add($" get {{ return ReadProperty({thePropName}Property); }}")
            Else
                ret.Add($" get {{ return ReadPropertyConvert<{theFieldType},{thePropType}>({thePropName}Property); }}")
            End If

            If thePropType <> theFieldType Then
                ret.Add($"private set {{ LoadPropertyConvert<{theFieldType},{thePropType}>({thePropName}Property, value); }}")
            Else
                ret.Add($"private set {{ LoadProperty({thePropName}Property, value); }}")
            End If


            ret.Add("}")

            Return ret

        End Function

        Private Function GetCellInfoAttribute() As String

            Dim attr = New List(Of String)
            If Not String.IsNullOrEmpty(LookupCode) Then attr.Add($"LookupCode=""{LookupCode}""")
            If Not String.IsNullOrEmpty(ControlType) AndAlso ControlType <> pbs.BO.Forms.CtrlType.NA Then attr.Add($"ControlType=CtrlType.{ControlType.ToString}")
            If Not String.IsNullOrEmpty(GroupName) Then attr.Add($"GroupName=""{GroupName}""")
            If Not String.IsNullOrEmpty(Tips) Then attr.Add($"Tips=""{Tips}""")
            If Not String.IsNullOrEmpty(InputOptions) Then attr.Add($"InputOptions=""{InputOptions}""")
            If IsHidden Then attr.Add($"Hidden=true")

            If attr.Count > 0 Then
                Return $"[CellInfo({String.Join(",", attr.ToArray)})]"
            End If

            Return Nothing

        End Function

        Private Function GetRuleAttribute() As String

            Dim attr = New List(Of String)
            If IsRequired Then attr.Add("Required=true")

            If Not String.IsNullOrEmpty(RegexRule) Then attr.Add($"RegexRule=@""{RegexRule}""")

            If attr.Count > 0 Then
                Return $"[Rule({String.Join(",", attr.ToArray)})]"
            End If

            Return Nothing

        End Function

        Private Function GetDBAttribute() As String

            Dim attr = New List(Of String)

            If Not String.IsNullOrEmpty(FieldStart) AndAlso FieldStart.ToInteger > 0 Then attr.Add($"Start={FieldStart}")
            If Not String.IsNullOrEmpty(FieldLen) AndAlso FieldLen.ToInteger > 0 Then attr.Add($"Len={FieldLen}")
            If Not String.IsNullOrEmpty(Me.DatabaseFieldName) AndAlso FieldLen.ToInteger > 0 Then attr.Add($"ColumnName=""{DatabaseFieldName}""")

            If attr.Count > 0 Then
                Return $"[DB({String.Join(",", attr.ToArray)})]"
            End If

            Return Nothing

        End Function



#Region "Service"

        Function GetDefaultParameterValue() As String
            Select Case Me.PropType
                Case "SmartDate", "SD"
                    Return "new SmartDate(true)"
                Case "SmartTime", "ST"
                    Return "new SmartTime()"
                Case "SmartPeriod", "SP"
                    Return "new SmartPeriod()"
                Case "SmartFloat", "SF"
                    Return "new SmartFloat(0)"
                Case "SmartInt32", "SI"
                    Return "new SmartInt32(0)"
                Case "SmartInt16", "SI16"
                    Return "new SmartInt16(0)"
                Case "String", "Char"
                    Return <t>""</t>.Value
                Case "int", "int32", "integer", "decimal"
                    Return "0"
                Case Else
                    Return <t>""</t>.Value
            End Select
        End Function

        Friend Function AssigningKeysValueToClone() As String

            Return <t>cloning.<%= FieldName %> = p<%= FieldName %>; </t>.Value

        End Function

        Friend Function AddingKeytoDictionary() As String
            If DBType.MatchesRegExp("^int") Then
                Return <t>pFilters.Add(nameof(<%= FieldName %>), <%= FieldName %>.ToString()); </t>.Value
            Else
                Return <t>pFilters.Add(nameof(<%= FieldName %>), <%= FieldName %>); </t>.Value
            End If

        End Function

        Friend Function AssigningKeysValueFromCriteria() As String

            If DBType.MatchesRegExp("^int") Then
                Return <t><%= FieldName %> = criteria.<%= FieldName %>.ToString(); </t>.Value
            Else
                Return <t><%= FieldName %> = criteria.<%= FieldName %>; </t>.Value
            End If


        End Function

        Friend Function AssigningValueFromDefaultValue() As String

            If PropType.MatchesRegExp("^int$|integer") Then
                Return <t><%= FieldName %> = <%= If(DefaultValue Is Nothing, "0", DefaultValue.ToString.ToInteger) %>; </t>.Value

            ElseIf PropType.MatchesRegExp("^decimal|^float") Then
                Return <t><%= FieldName %> = <%= If(DefaultValue Is Nothing, "0", DefaultValue.ToString.ToDecimal) %>; </t>.Value

            ElseIf PropType.MatchesRegExp("^bool") Then

                Return <t><%= FieldName %> = <%= If(DefaultValue Is Nothing, "false", DefaultValue.ToString.ToBoolean.ToString.ToLower) %>; </t>.Value

            ElseIf PropType.MatchesRegExp("^date") Then

                Return <t><%= FieldName %> = <%= If(DefaultValue Is Nothing, "new DateTime()", $"DateTime.Parse({DefaultValue.ToString})") %>; </t>.Value

            ElseIf Not IsChildCollection Then
                Return <t><%= FieldName %> = <%= If(DefaultValue Is Nothing, "string.Empty", Nz(DefaultValue.ToString, "string.Empty")) %>; </t>.Value
            End If


        End Function

        Friend Function GetFetchProperty411FromCriteria() As String
            'Dim ret = New List(Of String)

            Dim theFieldType = Mapper.FieldType411(FieldType)

            Dim thePropType = PropType.ToLower()

            Dim thePropName = FieldName


            'Return $"  {thePropName} = {pField.SQL_Read_Field};"
            If theFieldType <> thePropType Then
                Return $"  LoadPropertyConvert<{theFieldType}, {thePropType}>({thePropName}Property, p{FieldName});"
            Else
                Return $"  LoadProperty({thePropName}Property, p{FieldName});"
            End If


            'Return ret
        End Function

        Friend Function GetFetchProperty411() As String
            'Dim ret = New List(Of String)

            Dim theFieldType = Mapper.FieldType411(FieldType)

            Dim thePropType = PropType

            Dim thePropName = FieldName

            Dim theDBColumnName = DatabaseFieldName

            Dim theDBType = Safe_Read_FieldType()

            'Return $"  {thePropName} = {pField.SQL_Read_Field};"
            If FieldStart > 0 AndAlso FieldLen > 0 Then

                Return $"ClassSchema<{ClassName}>.GetSubData(nameof({thePropName}), _{DatabaseFieldName});"

            ElseIf theFieldType <> theDBType Then
                Return $"{If(String.IsNullOrEmpty(DatabaseFieldName), "//", "")} LoadPropertyConvert<{theFieldType}, {theDBType}>({thePropName}Property, {SQL_Read_Field()});"
            Else
                Return $" {If(String.IsNullOrEmpty(DatabaseFieldName), "//", "")} LoadProperty({thePropName}Property, {SQL_Read_Field()});"
            End If


            'Return ret
        End Function

        Friend Function GetFetchPropertyRFMSC411() As String

            Dim thePropType = PropType
            Dim thePropName = FieldName

            If Me.IsPrimaryKey Then Return String.Empty

            If thePropName.Equals("Dtb", StringComparison.OrdinalIgnoreCase) Then Return String.Empty
            If thePropName.Equals("Updated") Then Return String.Empty

            If FieldStart > 0 AndAlso FieldLen > 0 Then
                If PropType.MatchesRegExp("bool") Then
                    Return $" {thePropName} = ClassSchema<{ClassName}>.GetSubData(nameof({thePropName}), _data).ToBoolean();"

                ElseIf PropType.MatchesRegExp("int") Then
                    Return $" {thePropName} = ClassSchema<{ClassName}>.GetSubData(nameof({thePropName}), _data).ToInteger();"

                ElseIf PropType.MatchesRegExp("decimal|float") Then
                    Return $" {thePropName} = ClassSchema<{ClassName}>.GetSubData(nameof({thePropName}), _data).ToDecimal();"

                Else
                    Return $" {thePropName} = ClassSchema<{ClassName}>.GetSubData(nameof({thePropName}), _data).TrimEnd();"
                End If

            ElseIf PropType.MatchesRegExp("bool") Then
                    Return <d><%= thePropName %> = xele.GetString(nameof(<%= thePropName %>).GuessFieldName()).ToBoolean();</d>.Value
                ElseIf PropType.MatchesRegExp("decimal|float") Then
                    Return <d><%= thePropName %> = xele.GetString(nameof(<%= thePropName %>).GuessFieldName()).ToDecimal();</d>.Value
                ElseIf PropType.MatchesRegExp("^int") Then
                    Return <d><%= thePropName %> = xele.GetString(nameof(<%= thePropName %>).GuessFieldName()).ToInteger();</d>.Value
                Else
                    Return <d><%= thePropName %> = xele.GetString(nameof(<%= thePropName %>).GuessFieldName()).TrimEnd();</d>.Value
            End If

        End Function

        Private Function Safe_Read_FieldType() As String
            If DBType Is Nothing Then
                Return "string"

            ElseIf Regex.IsMatch(DBType, "CHAR", RegexOptions.IgnoreCase) Then
                Return "string"

            ElseIf Regex.IsMatch(DBType, "uniqueidentifier", RegexOptions.IgnoreCase) Then
                Return "string"

            ElseIf Regex.IsMatch(DBType, "SmallInt", RegexOptions.IgnoreCase) Then
                Return "int"

            ElseIf Regex.IsMatch(DBType, "tinyint", RegexOptions.IgnoreCase) Then
                Return "int"

            ElseIf Regex.IsMatch(DBType, "INT", RegexOptions.IgnoreCase) Then
                Return "int"

            ElseIf Regex.IsMatch(DBType, "Decimal", RegexOptions.IgnoreCase) Then
                Return "decimal"

            ElseIf Regex.IsMatch(DBType, "Numeric", RegexOptions.IgnoreCase) Then
                Return "decimal"

            ElseIf Regex.IsMatch(DBType, "DateTime", RegexOptions.IgnoreCase) Then
                Return "DateTime"

            ElseIf Regex.IsMatch(DBType, "bit", RegexOptions.IgnoreCase) Then
                Return "bool"

            Else
                Return "string"
            End If
        End Function

        Private Function SQL_Read_Field() As String


            If DBType Is Nothing Then
                Return <txt>dr.GetString("<%= DatabaseFieldName %>").TrimEnd()</txt>.Value

            ElseIf Regex.IsMatch(DBType, "CHAR", RegexOptions.IgnoreCase) Then
                Return <txt>dr.GetString("<%= DatabaseFieldName %>").TrimEnd()</txt>.Value

            ElseIf Regex.IsMatch(DBType, "uniqueidentifier", RegexOptions.IgnoreCase) Then
                Return <txt>dr.GetGuid("<%= DatabaseFieldName %>").ToString()</txt>.Value

            ElseIf Regex.IsMatch(DBType, "SmallInt", RegexOptions.IgnoreCase) Then
                Return <txt>dr.GetInt16("<%= DatabaseFieldName %>")</txt>.Value

            ElseIf Regex.IsMatch(DBType, "tinyint", RegexOptions.IgnoreCase) Then
                Return <txt>dr.GetByte("<%= DatabaseFieldName %>")</txt>.Value

            ElseIf Regex.IsMatch(DBType, "INT", RegexOptions.IgnoreCase) Then
                Return <txt>dr.GetInt32("<%= DatabaseFieldName %>")</txt>.Value

            ElseIf Regex.IsMatch(DBType, "Decimal", RegexOptions.IgnoreCase) Then
                Return <txt>dr.GetDecimal("<%= DatabaseFieldName %>")</txt>.Value

            ElseIf Regex.IsMatch(DBType, "Numeric", RegexOptions.IgnoreCase) Then
                Return <txt>dr.GetDecimal("<%= DatabaseFieldName %>")</txt>.Value

            ElseIf Regex.IsMatch(DBType, "DateTime", RegexOptions.IgnoreCase) Then
                Return <txt>dr.GetDateTime("<%= DatabaseFieldName %>")</txt>.Value

            ElseIf Regex.IsMatch(DBType, "bit", RegexOptions.IgnoreCase) Then
                Return <txt>dr.GetBoolean("<%= DatabaseFieldName %>")</txt>.Value

            Else
                Return <txt>dr.GetString("<%= DatabaseFieldName %>").TrimEnd()</txt>.Value
            End If
        End Function

        Friend Function InsertUpdateToDBRFMSC() As String

            Dim thePropName = FieldName

            If thePropName.Equals("Dtb", StringComparison.OrdinalIgnoreCase) Then Return String.Empty

            If DatabaseFieldName = "UPDATED" Then Return String.Empty

            If String.IsNullOrEmpty(DatabaseFieldName) Then

                If FieldStart > 0 AndAlso FieldLen > 0 Then

                    Return $"ClassSchema<{ClassName}>.SetSubData(nameof({thePropName}), ReadProperty({thePropName}Property), ref _data);"

                ElseIf FieldType.MatchesRegExp("SmartDate") Then
                    Return <t>  _data.AddWithValue("<%= thePropName %>".GuessFieldName(), ReadProperty(<%= thePropName %>Property).DBValue.ToString()); </t>.Value

                ElseIf FieldType.MatchesRegExp("SmartTime") Then
                    Return <t>  _data.AddWithValue("<%= thePropName %>".GuessFieldName(), ReadProperty(<%= thePropName %>Property).DBValue.ToString()); </t>.Value

                ElseIf FieldType.MatchesRegExp("SmartPeriod") Then
                    Return <t>  _data.AddWithValue("<%= thePropName %>".GuessFieldName(), ReadProperty(<%= thePropName %>Property).DBValueInt.ToString()); </t>.Value

                ElseIf FieldType.MatchesRegExp("int") Then
                    Return <t>  _data.AddWithValue("<%= thePropName %>".GuessFieldName(), ReadProperty(<%= thePropName %>Property)); </t>.Value

                Else
                    Return <t>  _data.AddWithValue("<%= thePropName %>".GuessFieldName(), ReadProperty(<%= thePropName %>Property).TrimEnd()); </t>.Value

                End If

            Else

                If FieldStart > 0 AndAlso FieldLen > 0 Then
                    Return $"ClassSchema<{ClassName}>.SetSubData(nameof({thePropName}), ReadProperty({thePropName}Property), ref _{DatabaseFieldName});"

                ElseIf FieldType.MatchesRegExp("SmartDate") Then
                    Return <t>  _data.AddWithValue("@<%= DatabaseFieldName %>", ReadProperty(<%= thePropName %>Property).DBValue.ToString()); </t>.Value

                ElseIf FieldType.MatchesRegExp("SmartTime") Then
                    Return <t>  _data.AddWithValue("@<%= DatabaseFieldName %>", ReadProperty(<%= thePropName %>Property).DBValue.ToString()); </t>.Value

                ElseIf FieldType.MatchesRegExp("SmartPeriod") Then
                    Return <t>  _data.AddWithValue("@<%= DatabaseFieldName %>", ReadProperty(<%= thePropName %>Property).DBValueInt.ToString()); </t>.Value

                ElseIf FieldType.MatchesRegExp("int") Then

                    Return <t>  _data.AddWithValue("@<%= DatabaseFieldName %>", ReadProperty(<%= thePropName %>Property)); </t>.Value
                Else

                    Return <t>  _data.AddWithValue("@<%= DatabaseFieldName %>", ReadProperty(<%= thePropName %>Property).TrimEnd()); </t>.Value

                End If

            End If


        End Function

        Friend Function InsertUpdateToDB() As String
            Dim theFieldType = Mapper.FieldType411(FieldType)

            Dim thePropName = FieldName

            Dim theDBType = Safe_Read_FieldType()

            If FieldStart > 0 AndAlso FieldLen > 0 Then

                Return $"ClassSchema<{ClassName}>.SetSubData(nameof({thePropName}), ReadProperty({thePropName}Property), ref _{Nz(DatabaseFieldName, "_data")});"

            ElseIf theFieldType <> theDBType Then
                Return <t><%= If(String.IsNullOrEmpty(DatabaseFieldName), "//", "") %> cm.Parameters.AddWithValue("@<%= DatabaseFieldName %>", ReadPropertyConvert&lt;<%= theFieldType %>, <%= theDBType %>>(<%= thePropName %>Property)); </t>.Value
            Else
                Return <t><%= If(String.IsNullOrEmpty(DatabaseFieldName), "//", "") %> cm.Parameters.AddWithValue("@<%= DatabaseFieldName %>", ReadProperty(<%= thePropName %>Property)); </t>.Value
            End If

        End Function
#End Region

    End Class


End Namespace