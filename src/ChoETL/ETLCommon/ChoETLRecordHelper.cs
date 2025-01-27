﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChoETL
{
    public static class ChoETLRecordHelper
    {
        public static object CreateInstanceAndDefaultToMembers(this Type type, IDictionary<string, ChoRecordFieldConfiguration> fcs)
        {
            var obj = ChoActivator.CreateInstance(type);
            object defaultValue = null;
            foreach (PropertyDescriptor pd in ChoTypeDescriptor.GetProperties(type))
            {
                try
                {
                    if (!fcs.ContainsKey(pd.Name) || !fcs[pd.Name].IsDefaultValueSpecified)
                        continue;

                    defaultValue = fcs[pd.Name].DefaultValue;
                    if (defaultValue != null)
                        ChoType.ConvertNSetPropertyValue(obj, pd.Name, defaultValue);
                }
                catch (Exception ex)
                {
                    ChoETLFramework.WriteLog(ChoETLFramework.TraceSwitch.TraceError, "Error while assigning default value '{0}' to '{1}' member. {2}".FormatString(defaultValue, ChoType.GetMemberName(pd), ex.Message));
                }
            }
            return obj;
        }

        public static bool IgnoreFieldValue(this object fieldValue, ChoIgnoreFieldValueMode? ignoreFieldValueMode)
        {
            if (ignoreFieldValueMode == null)
                return false; // fieldValue == null;

            if ((ignoreFieldValueMode & ChoIgnoreFieldValueMode.Null) == ChoIgnoreFieldValueMode.Null && fieldValue == null)
                return true;
            else if ((ignoreFieldValueMode & ChoIgnoreFieldValueMode.DBNull) == ChoIgnoreFieldValueMode.DBNull && fieldValue == DBNull.Value)
                return true;
            else if ((ignoreFieldValueMode & ChoIgnoreFieldValueMode.Empty) == ChoIgnoreFieldValueMode.Empty && fieldValue is string && ((string)fieldValue).IsEmpty())
                return true;
            else if ((ignoreFieldValueMode & ChoIgnoreFieldValueMode.WhiteSpace) == ChoIgnoreFieldValueMode.WhiteSpace && fieldValue is string && ((string)fieldValue).IsNullOrWhiteSpace())
                return true;

            return false;
        }

        public static Type ResolveRecordType(this Type recordType)
        {
            if (typeof(ICollection).IsAssignableFrom(recordType)
                || recordType.IsSimple())
                throw new ChoParserException("Invalid record type passed.");
            else
                return recordType.GetUnderlyingType();
        }

        public static void ConvertNSetMemberValue(this IDictionary<string, object> dict, string fn, ChoRecordFieldConfiguration fieldConfig, ref object fieldValue, CultureInfo culture,
            ChoRecordConfiguration config = null)
        {
            ChoDynamicObject dDict = dict as ChoDynamicObject;
            if (fieldValue is ChoDynamicObject)
                ((ChoDynamicObject)fieldValue).DynamicObjectName = fn;

            if (fieldConfig.ValueConverter != null)
                fieldValue = fieldConfig.ValueConverter(fieldValue);
            else
            {
                object[] fcParams = fieldConfig.PropConverterParams;
                if (!fieldConfig.FormatText.IsNullOrWhiteSpace())
                    fcParams = new object[] { new object[] { fieldConfig.FormatText } };

                if (fieldConfig.Converters.IsNullOrEmpty())
                {
                    Type fieldType = fieldConfig.SourceType == null ? fieldConfig.FieldType : fieldConfig.SourceType;
                    var ft = fieldValue == null ? fieldType : fieldValue.GetType();
                    object[] convs = fieldConfig.PropConverters;
                    if (convs.IsNullOrEmpty() && config != null)
                    {
                        var convs1 = config.GetConvertersForType(ft);
                        if (!convs1.IsNullOrEmpty())
                        {
                            convs = convs1;
                            fcParams = GetPropertyConvertersParams(config.GetConverterParamsForType(ft), fieldConfig.FormatText);
                        }
                    }
                    else
                    {

                    }
                    fieldValue = ChoConvert.ConvertFrom(fieldValue, fieldConfig.FieldType == null ? typeof(object) : fieldConfig.FieldType, null, convs, fcParams, culture);
                }
                else
                    fieldValue = ChoConvert.ConvertFrom(fieldValue, fieldConfig.FieldType == null ? typeof(object) : fieldConfig.FieldType, null, fieldConfig.Converters.ToArray(), fcParams, culture);
            }

            if (dDict != null)
                dDict.AddToDictionary(fn, fieldValue);
            else
                dict.AddOrUpdate(fn, fieldValue);

            if (dDict != null && fieldValue == null && fieldConfig.FieldType != null)
            {
                dDict.SetMemberType(fn, fieldConfig.FieldType);
            }

        }

        public static bool ConvertMemberValue(this object rec, string fn, ChoRecordFieldConfiguration fieldConfig, ref object fieldValue, CultureInfo culture,
            ChoRecordConfiguration config = null)
        {
            if (fieldConfig.PD == null)
                fieldConfig.PD = fieldConfig.PropertyDescriptor;

            if (fieldConfig.PD == null) return false;

            if (fieldValue is ChoDynamicObject)
                ((ChoDynamicObject)fieldValue).DynamicObjectName = fn;

            if (fieldConfig.ValueConverter != null)
                fieldValue = fieldConfig.ValueConverter(fieldValue);
            else
            {
                if (fieldConfig.Converters.IsNullOrEmpty())
                {
                    object[] fcParams = GetPropertyConvertersParams(fieldConfig);
                    Type fieldType = fieldConfig.PD != null ? fieldConfig.PD.PropertyType : null;
                    object[] convs = fieldConfig.PropConverters;
                    if (convs.IsNullOrEmpty() && config != null)
                    {
                        var convs1 = config.GetConvertersForType(fieldType);
                        if (!convs1.IsNullOrEmpty())
                        {
                            convs = convs1;
                            fcParams = GetPropertyConvertersParams(config.GetConverterParamsForType(fieldType), fieldConfig.FormatText);
                        }
                    }
                    if (!convs.IsNullOrEmpty())
                    {
                        fieldValue = ChoConvert.ConvertFrom(fieldValue, fieldConfig.FieldType == null ? typeof(object) : fieldConfig.FieldType, null, convs, fcParams, culture);
                        return true;
                    }
                }

                if (fieldValue != null && fieldConfig.PD.PropertyType != null && fieldConfig.PD.PropertyType != typeof(object)
                    && fieldConfig.PD.PropertyType.IsAssignableFrom(fieldValue.GetType()))
                {

                }
                else if (fieldConfig.PD.PropertyType.IsGenericType && (fieldConfig.PD.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>) || fieldConfig.PD.PropertyType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                        /*&& typeof(string) == fieldConfig.PD.PropertyType.GetGenericArguments()[0]*/)
                {
                    //IDictionary dict = ChoType.GetPropertyValue(rec, fieldConfig.PD) as IDictionary;
                    IDictionary dict = fieldConfig.PD.GetValue(rec) as IDictionary;
                    if (dict == null)
                    {
                        dict = (IDictionary)Activator.CreateInstance(fieldConfig.FieldType);
                        fieldConfig.PD.SetValue(rec, dict);
                        //ChoType.SetPropertyValue(rec, fieldConfig.PD, dict);
                    }

                    if (fieldConfig is ChoFileRecordFieldConfiguration && !((ChoFileRecordFieldConfiguration)fieldConfig).DictKey.IsNullOrWhiteSpace())
                    {
                        if (!dict.Contains(fn))
                        {
                            var valueType = fieldConfig.PD.PropertyType.GetGenericArguments()[1];
                            object[] fcParams = GetPropertyConvertersParams(fieldConfig);
                            if (fieldConfig.ValueConverters.IsNullOrEmpty())
                                fieldValue = ChoConvert.ConvertFrom(fieldValue, valueType, null, null, fcParams, culture);
                            else
                                fieldValue = ChoConvert.ConvertFrom(fieldValue, valueType, null, fieldConfig.ValueConverters.ToArray(), fcParams, culture);

                            dict.Add(fn, fieldValue);
                        }
                    }
                    else
                    {
                        object[] fcParams = GetPropertyConvertersParams(fieldConfig);
                        if (fieldConfig.Converters.IsNullOrEmpty())
                        {
                            if (fieldConfig.PropConverters.IsNullOrEmpty())
                            {
                                var keyType = fieldConfig.PD.PropertyType.GetGenericArguments()[0];
                                var valueType = fieldConfig.PD.PropertyType.GetGenericArguments()[1];

                                char itemSeparator = ';';
                                char keyValueSeparator = '=';

                                if (fieldConfig is ChoCSVRecordFieldConfiguration)
                                {
                                    var fc = fieldConfig as ChoCSVRecordFieldConfiguration;
                                    if (!fc.ItemSeparator.IsNull())
                                        itemSeparator = fc.ItemSeparator;
                                    if (!fc.KeyValueSeparator.IsNull())
                                        keyValueSeparator = fc.KeyValueSeparator;
                                }

                                if (fieldValue is string)
                                {
                                    object key = null;
                                    object value = null;
                                    foreach (var kvp in ((string)fieldValue).ToKeyValuePairs(itemSeparator, keyValueSeparator))
                                    {
                                        key = null;
                                        value = null;
                                        if (fieldConfig.KeyConverters.IsNullOrEmpty())
                                            key = ChoConvert.ConvertFrom(kvp.Key, keyType, null, null, fcParams, culture);
                                        else
                                            key = ChoConvert.ConvertFrom(kvp.Key, keyType, null, fieldConfig.KeyConverters.ToArray(), fcParams, culture);
                                        if (fieldConfig.ValueConverters.IsNullOrEmpty())
                                            value = ChoConvert.ConvertFrom(kvp.Value, valueType, null, null, fcParams, culture);
                                        else
                                            value = ChoConvert.ConvertFrom(kvp.Value, valueType, null, fieldConfig.ValueConverters.ToArray(), fcParams, culture);

                                        if (key != null)
                                            dict.Add(key, value);
                                    }
                                }
                                else if (fieldValue != null && typeof(IDictionary<string, object>).IsAssignableFrom(fieldValue.GetType()))
                                {
                                    object key1 = null;
                                    object value1 = null;
                                    foreach (var kvp in ((IDictionary<string, object>)fieldValue))
                                    {
                                        if (fieldConfig.KeyConverters.IsNullOrEmpty())
                                            key1 = ChoConvert.ConvertFrom(kvp.Key, keyType, null, null, fcParams, culture);
                                        else
                                            key1 = ChoConvert.ConvertFrom(kvp.Key, keyType, null, fieldConfig.KeyConverters.ToArray(), fcParams, culture);
                                        if (fieldConfig.ValueConverters.IsNullOrEmpty())
                                            value1 = ChoConvert.ConvertFrom(kvp.Value, valueType, null, null, fcParams, culture);
                                        else
                                            value1 = ChoConvert.ConvertFrom(kvp.Value, valueType, null, fieldConfig.ValueConverters.ToArray(), fcParams, culture);

                                        if (key1 != null)
                                            dict.Add(key1, value1);
                                    }
                                }
                                else if (fieldValue is IDictionary)
                                {
                                    object key1 = null;
                                    object value1 = null;
                                    foreach (var lkey in ((IDictionary)fieldValue).Keys)
                                    {
                                        if (fieldConfig.KeyConverters.IsNullOrEmpty())
                                            key1 = ChoConvert.ConvertFrom(lkey, keyType, null, null, fcParams, culture);
                                        else
                                            key1 = ChoConvert.ConvertFrom(lkey, keyType, null, fieldConfig.KeyConverters.ToArray(), fcParams, culture);
                                        if (fieldConfig.ValueConverters.IsNullOrEmpty())
                                            value1 = ChoConvert.ConvertFrom(((IDictionary)fieldValue)[lkey], valueType, null, null, fcParams, culture);
                                        else
                                            value1 = ChoConvert.ConvertFrom(((IDictionary)fieldValue)[lkey], valueType, null, fieldConfig.ValueConverters.ToArray(), fcParams, culture);

                                        if (key1 != null)
                                            dict.Add(key1, value1);
                                    }
                                }
                            }
                            else
                                fieldValue = ChoConvert.ConvertFrom(fieldValue, fieldConfig.PD.PropertyType, null, fieldConfig.PropConverters, fcParams, culture);
                        }
                        else
                            fieldValue = ChoConvert.ConvertFrom(fieldValue, fieldConfig.PD.PropertyType, null, fieldConfig.Converters.ToArray(), fcParams, culture);
                    }

                    return false;
                }
                else if (typeof(IList).IsAssignableFrom(fieldConfig.PD.PropertyType))
                {
                    var itemType = fieldConfig.PD.PropertyType.GetItemType();
                    //IList list = ChoType.GetPropertyValue(rec, fieldConfig.PD) as IList;
                    IList list = fieldConfig.PD.GetValue(rec) as IList;
                    if (list == null && !fieldConfig.PD.PropertyType.IsArray) //fieldConfig.FieldType.IsArray)
                    {
                        list = (IList)Activator.CreateInstance(fieldConfig.PD.PropertyType); // fieldConfig.FieldType);
                        //ChoType.SetPropertyValue(rec, fieldConfig.PD, list);
                        fieldConfig.PD.SetValue(rec, list);
                    }

                    if (((ChoFileRecordFieldConfiguration)fieldConfig).ArrayIndex != null)
                    {
                        if (list != null)
                        {
                            if (itemType != null)
                            {
                                object[] fcParams = GetPropertyConvertersParams(fieldConfig);
                                if (fieldConfig.ItemConverters.IsNullOrEmpty())
                                    fieldValue = ChoConvert.ConvertFrom(fieldValue, itemType, null, null, fcParams, culture);
                                else
                                    fieldValue = ChoConvert.ConvertFrom(fieldValue, itemType, null, fieldConfig.ItemConverters.ToArray(), fcParams, culture);
                            }

                            if (list.IsFixedSize)
                            {
                                int ai = fieldConfig is ChoFileRecordFieldConfiguration ? ((ChoFileRecordFieldConfiguration)fieldConfig).ArrayIndex != null ? ((ChoFileRecordFieldConfiguration)fieldConfig).ArrayIndex.Value : -1 : -1;
                                if (ai >= 0 && ai < ((Array)list).Length)
                                    ((Array)list).SetValue(fieldValue, ai);
                            }
                            else
                            {
                                list.Add(fieldValue);
                            }
                        }
                        return false;
                    }
                    else
                    {
                        object[] fcParams = GetPropertyConvertersParams(fieldConfig);
                        if (fieldConfig.Converters.IsNullOrEmpty())
                        {
                            if (fieldConfig.PropConverters.IsNullOrEmpty() && fieldValue is string)
                            {
                                List<object> result = new List<object>();
                                if (itemType != null)
                                {
                                    char itemSeparator = ';';

                                    if (fieldConfig is ChoCSVRecordFieldConfiguration)
                                    {
                                        var fc = fieldConfig as ChoCSVRecordFieldConfiguration;
                                        if (!fc.ItemSeparator.IsNull())
                                            itemSeparator = fc.ItemSeparator;
                                    }

                                    foreach (var item in ((string)fieldValue).SplitNTrim(itemSeparator))
                                    {
                                        if (fieldConfig.ItemConverters.IsNullOrEmpty())
                                            result.Add(ChoConvert.ConvertFrom(item, itemType, null, null, fcParams, culture));
                                        else
                                            result.Add(ChoConvert.ConvertFrom(item, itemType, null, fieldConfig.ItemConverters.ToArray(), fcParams, culture));
                                    }
                                }
                                if (fieldConfig.PD.PropertyType.IsArray)
                                    fieldValue = result.ToArray();
                                else
                                    fieldValue = result;
                            }
                            else
                                fieldValue = ChoConvert.ConvertFrom(fieldValue, fieldConfig.PD.PropertyType, null, fieldConfig.PropConverters, fcParams, culture);
                        }
                        else
                            fieldValue = ChoConvert.ConvertFrom(fieldValue, fieldConfig.PD.PropertyType, null, fieldConfig.Converters.ToArray(), fcParams, culture);
                    }
                }
                else
                {
                    object[] fcParams = GetPropertyConvertersParams(fieldConfig);
                    if (fieldConfig.Converters.IsNullOrEmpty())
                        fieldValue = ChoConvert.ConvertFrom(fieldValue, fieldConfig.PD.PropertyType, null, fieldConfig.PropConverters, fcParams, culture);
                    else
                        fieldValue = ChoConvert.ConvertFrom(fieldValue, fieldConfig.PD.PropertyType, null, fieldConfig.Converters.ToArray(), fcParams, culture);
                }
            }
            return true;
        }

        public static void ConvertNSetMemberValue(this object rec, string fn, ChoRecordFieldConfiguration fieldConfig, ref object fieldValue, CultureInfo culture,
            ChoRecordConfiguration config = null)
        {
            if (fieldConfig.PD == null)
                fieldConfig.PD = fieldConfig.PropertyDescriptor;

            if (fieldConfig.PD == null) return;

            if (ConvertMemberValue(rec, fn, fieldConfig, ref fieldValue, culture, config))
            {
                fieldConfig.PD.SetValue(rec, fieldValue);
                //ChoType.SetPropertyValue(rec, fieldConfig.PI, fieldValue);
            }
        }

        private static object[] GetPropertyConvertersParams(ChoRecordFieldConfiguration fieldConfig)
        {
            object[] fcParams = fieldConfig.PropConverterParams;
            if (!fieldConfig.FormatText.IsNullOrWhiteSpace())
                fcParams = new object[] { new object[] { fieldConfig.FormatText } };

            return fcParams;
        }
        private static object[] GetPropertyConvertersParams(object[] parameters, string formatText)
        {
            object[] fcParams = parameters;
            if (!formatText.IsNullOrWhiteSpace())
                fcParams = new object[] { new object[] { formatText } };

            return fcParams;
        }

        public static bool SetFallbackValue(this object rec, string fn, ChoRecordFieldConfiguration fieldConfig, CultureInfo culture)
        {
            if (!fieldConfig.IsFallbackValueSpecified)
                return false;

            if (rec is IDictionary<string, object>)
            {
                return ((IDictionary<string, object>)rec).SetFallbackValue(fn, fieldConfig, culture);
            }
            if (fieldConfig.PD == null)
                fieldConfig.PD = fieldConfig.PropertyDescriptor;

            //Set fallback value to member
            if (fieldConfig.PD != null)
            {
                object fieldValue = null;
                if (fieldConfig.Converters.IsNullOrEmpty())
                    fieldValue = ChoConvert.ConvertFrom(fieldConfig.FallbackValue, fieldConfig.PD.PropertyType, null, fieldConfig.PropConverters, fieldConfig.PropConverterParams, culture);
                else
                    fieldValue = ChoConvert.ConvertFrom(fieldConfig.FallbackValue, fieldConfig.PD.PropertyType, null, fieldConfig.Converters.ToArray(), null, culture);

                //ChoType.SetPropertyValue(rec, fieldConfig.PD, fieldValue);
                fieldConfig.PD.SetValue(rec, fieldValue);
            }
            return true;
        }

        public static bool SetDefaultValue(this object rec, string fn, ChoRecordFieldConfiguration fieldConfig, CultureInfo culture)
        {
            if (!fieldConfig.IsDefaultValueSpecified)
                return false;

            if (rec is IDictionary<string, object>)
            {
                return ((IDictionary<string, object>)rec).SetDefaultValue(fn, fieldConfig, culture);
            }
            if (fieldConfig.PD == null)
                fieldConfig.PD = fieldConfig.PropertyDescriptor;

            if (fieldConfig.PD != null)
            {
                //Set default value to member
                object fieldValue = null;
                if (fieldConfig.Converters.IsNullOrEmpty())
                    fieldValue = ChoConvert.ConvertFrom(fieldConfig.DefaultValue, fieldConfig.PD.PropertyType, null, fieldConfig.PropConverters, fieldConfig.PropConverterParams, culture);
                else
                    fieldValue = ChoConvert.ConvertFrom(fieldConfig.DefaultValue, fieldConfig.PD.PropertyType, null, fieldConfig.Converters.ToArray(), null, culture);

                //ChoType.SetPropertyValue(rec, fieldConfig.PI, fieldValue);
                fieldConfig.PD.SetValue(rec, fieldValue);
            }
            return true;
        }

        public static bool SetFallbackValue(this IDictionary<string, object> dict, string fn, ChoRecordFieldConfiguration fieldConfig, CultureInfo culture, ref object fallbackValue)
        {
            //Set Fallback value to member
            if (fieldConfig.IsFallbackValueSpecified)
            {
                if (fieldConfig.Converters.IsNullOrEmpty())
                    fallbackValue = ChoConvert.ConvertFrom(fieldConfig.FallbackValue, fieldConfig.FieldType, null, fieldConfig.PropConverters, fieldConfig.PropConverterParams, culture);
                else
                    fallbackValue = ChoConvert.ConvertFrom(fieldConfig.FallbackValue, fieldConfig.FieldType, null, fieldConfig.Converters.ToArray(), null, culture);

                dict.AddOrUpdate(fn, fallbackValue);
            }

            return fieldConfig.IsFallbackValueSpecified;
        }

        public static bool SetDefaultValue(this IDictionary<string, object> dict, string fn, ChoRecordFieldConfiguration fieldConfig, CultureInfo culture)
        {
            object fieldValue = null;
            //Set default value to member
            if (fieldConfig.IsDefaultValueSpecified)
            {
                if (fieldConfig.Converters.IsNullOrEmpty())
                    fieldValue = ChoConvert.ConvertFrom(fieldConfig.DefaultValue, fieldConfig.FieldType, null, fieldConfig.PropConverters, fieldConfig.PropConverterParams, culture);
                else
                    fieldValue = ChoConvert.ConvertFrom(fieldConfig.DefaultValue, fieldConfig.FieldType, null, fieldConfig.Converters.ToArray(), null, culture);

                dict.AddOrUpdate(fn, fieldValue);
            }
            else
            {
                dict.AddOrUpdate(fn, fieldConfig.FieldType.Default());
            }

            return fieldConfig.IsDefaultValueSpecified;
        }

        public static void DoObjectLevelValidation(this object recObject, ChoRecordConfiguration configuration, IEnumerable<ChoRecordFieldConfiguration> fieldConfigurations)
        {
            if (recObject == null)
                return;

            if ((configuration.ObjectValidationMode & ChoObjectValidationMode.ObjectLevel) == ChoObjectValidationMode.ObjectLevel)
            {
                if (configuration.Validator == null)
                {
                    if (configuration.HasConfigValidators)
                    {
                        IDictionary<string, Object> dict = null;
                        if (recObject is IDictionary<string, object>)
                            dict = recObject as IDictionary<string, Object>;
                        else
                        {
                            dict = new Dictionary<string, object>();

                            foreach (var pd in configuration.PIDict.Values)
                            {
                                dict.Add(pd.Name, ChoType.GetPropertyValue(recObject, pd));
                            }
                        }

                        ChoValidator.Validate(dict, configuration.ValDict);
                    }
                    else
                    {
                        if (!configuration.IsDynamicObject)
                            ChoValidator.Validate(recObject);
                    }
                }
                else
                {
                    if (recObject != null && configuration.Validator(recObject))
                        throw new ValidationException("Failed to validate '{0}' object. {1}".FormatString(recObject.GetType().FullName, Environment.NewLine));
                }
            }
        }

        public static void DoMemberLevelValidation(this IDictionary<string, object> dict, string fn, ChoRecordFieldConfiguration fieldConfig, ChoObjectValidationMode vm)
        {
            if (!fieldConfig.Validators.IsNullOrEmpty() && (vm & ChoObjectValidationMode.MemberLevel) == ChoObjectValidationMode.MemberLevel)
            {
                if (fieldConfig.Validator == null)
                    ChoValidator.ValidateFor(dict[fn], fn, fieldConfig.Validators);
                else
                {
                    if (!fieldConfig.Validator(dict[fn]))
                        throw new ValidationException("Failed to validate '{0}' member. {1}".FormatString(fn, Environment.NewLine));
                }
            }
        }

        public static void DoMemberLevelValidation(this object rec, string fn, ChoRecordFieldConfiguration fieldConfig, ChoObjectValidationMode vm)
        {
            if (!((vm & ChoObjectValidationMode.MemberLevel) == ChoObjectValidationMode.MemberLevel))
                return;

            if (rec is IDictionary<string, object>)
            {
                ((IDictionary<string, object>)rec).DoMemberLevelValidation(fn, fieldConfig, vm);
            }
            else
            {
                if (fieldConfig.Validator == null)
                {
                    if (fieldConfig.PD == null)
                        fieldConfig.PD = fieldConfig.PropertyDescriptor;
                    if (fieldConfig.PD != null)
                    {
                        if (fieldConfig.Validators.IsNullOrEmpty())
                            ChoValidator.ValidateFor(rec, fieldConfig.PD);
                        else
                            ChoValidator.ValidateFor(fieldConfig.PD.GetValue(rec), fn, fieldConfig.Validators);
                    }
                }
                else
                {
                    if (!fieldConfig.Validator(fieldConfig.PD.GetValue(rec)))
                        throw new ValidationException("Failed to validate '{0}' member. {1}".FormatString(fn, Environment.NewLine));
                }
            }
        }

        public static void DoMemberLevelValidation(this object rec, string fn, ChoRecordFieldConfiguration fieldConfig, ChoObjectValidationMode vm, object fieldValue)
        {
            if ((vm & ChoObjectValidationMode.MemberLevel) == ChoObjectValidationMode.MemberLevel)
            {
                if (fieldConfig.Validators.IsNullOrEmpty())
                {
                    ChoValidator.ValidateFor(fieldValue, fn, fieldConfig.Validators);
                }
            }
        }

        //*****
        public static bool GetDefaultValue(this object rec, string fn, ChoRecordFieldConfiguration fieldConfig, CultureInfo culture, ref object fieldValue)
        {
            if (!fieldConfig.IsDefaultValueSpecified)
                return false;

            if (!fieldConfig.FormatText.IsNullOrWhiteSpace())
                fieldValue = ("{0:" + fieldConfig.FormatText + "}").FormatString(fieldValue);
            else
            {
                if (fieldConfig.Converters.IsNullOrEmpty())
                    fieldValue = ChoConvert.ConvertTo(fieldConfig.DefaultValue, fieldConfig.FieldType, null, fieldConfig.PropConverters, fieldConfig.PropConverterParams, culture);
                else
                    fieldValue = ChoConvert.ConvertTo(fieldConfig.DefaultValue, fieldConfig.FieldType, null, fieldConfig.Converters.ToArray(), null, culture);
            }
            return true;
        }

        public static bool GetFallbackValue(this object rec, string fn, ChoRecordFieldConfiguration fieldConfig, CultureInfo culture, ref object fieldValue)
        {
            if (!fieldConfig.IsFallbackValueSpecified)
                return false;

            if (!fieldConfig.FormatText.IsNullOrWhiteSpace())
                fieldValue = ("{0:" + fieldConfig.FormatText + "}").FormatString(fieldValue);
            else
            {
                if (fieldConfig.Converters.IsNullOrEmpty())
                    fieldValue = ChoConvert.ConvertTo(fieldConfig.FallbackValue, fieldConfig.FieldType, null, fieldConfig.PropConverters, fieldConfig.PropConverterParams, culture);
                else
                    fieldValue = ChoConvert.ConvertTo(fieldConfig.FallbackValue, fieldConfig.FieldType, null, fieldConfig.Converters.ToArray(), null, culture);
            }
            return true;
        }

        public static void GetNConvertMemberValue(this object rec, string fn, ChoRecordFieldConfiguration fieldConfig, CultureInfo culture, ref object fieldValue, bool nativeType = false,
            ChoRecordConfiguration config = null)
        {
            if (!fieldConfig.FormatText.IsNullOrWhiteSpace() && fieldConfig.Converters.IsNullOrEmpty())
                fieldValue = ("{0:" + fieldConfig.FormatText + "}").FormatString(fieldValue);
            else
            {
                object[] fcParams = fieldConfig.PropConverterParams;
                if (!fieldConfig.FormatText.IsNullOrWhiteSpace())
                    fcParams = new object[] { new object[] { fieldConfig.FormatText } };

                if (fieldConfig.Converters.IsNullOrEmpty())
                {
                    Type fieldType = fieldConfig.SourceType == null ? fieldConfig.FieldType : fieldConfig.SourceType;
                    if (fieldConfig.PropConverters.IsNullOrEmpty())
                    {
                        var ft = nativeType ? fieldType : fieldValue == null ? typeof(string) : fieldValue.GetType();
                        object[] convs = fieldConfig.PropConverters;
                        if (convs.IsNullOrEmpty() && config != null)
                        {
                            var convs1 = config.GetConvertersForType(ft);
                            if (!convs1.IsNullOrEmpty())
                            {
                                convs = convs1;
                                fcParams = GetPropertyConvertersParams(config.GetConverterParamsForType(ft), fieldConfig.FormatText);
                            }
                        }
                        fieldValue = ChoConvert.ConvertTo(fieldValue, ft, null, convs, null, culture);
                    }
                    else
                        fieldValue = ChoConvert.ConvertTo(fieldValue, nativeType ? fieldType : typeof(string), null, fieldConfig.PropConverters, fcParams, culture);
                }
                else
                {
                    fieldValue = ChoConvert.ConvertTo(fieldValue, typeof(string), null, fieldConfig.Converters.ToArray(), fcParams, culture);
                }
            }
        }

        //public static void DoObjectLevelValidatation(this object record, ChoRecordConfiguration rc,  ChoRecordFieldConfiguration[] fieldConfigs)
        //{
        //    if (rc.HasConfigValidators)
        //    {
        //        Dictionary<string, ValidationAttribute[]> valDict = rc.ValDict;
        //        IDictionary<string, Object> dict = null;
        //        if (record is ExpandoObject)
        //            dict = record as IDictionary<string, Object>;
        //        else
        //            dict = record.ToDictionary();

        //        ChoValidator.Validate(dict, valDict);
        //    }
        //    else
        //    {
        //        if (!(record is ExpandoObject))
        //            ChoValidator.Validate(record);
        //    }
        //}

    }
}
