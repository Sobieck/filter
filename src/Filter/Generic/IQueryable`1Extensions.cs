using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RimDev.Filter.Range.Generic;

namespace RimDev.Filter.Generic
{
    public static class IQueryable_1Extensions
    {
        public static IQueryable<T> Filter<T>(
            this IQueryable<T> value,
            object filter)
        {
            if (filter == null)
            {
                return value;
            }

            var validValueProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead)
                .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

            var filterProperties = filter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead);

            var queryableValue = value;
            foreach (var filterProperty in filterProperties)
            {
                PropertyInfo validValueProperty;
                validValueProperties.TryGetValue(filterProperty.Name, out validValueProperty);

                var filterPropertyValue = filterProperty.GetValue(filter, null);
                if (validValueProperty != null && filterPropertyValue != null)
                {
                    var validValuePropertyName = validValueProperty.Name;

                    if (typeof(IEnumerable).IsAssignableFrom(filterProperty.PropertyType) &&
                        filterProperty.PropertyType != typeof(string))
                    {
                        object firstItem = null;
                        if (typeof(ICollection).IsAssignableFrom(filterProperty.PropertyType) &&
                            ((ICollection)filterPropertyValue).Count == 1)
                        {
                            Array array;
                            if (filterProperty.PropertyType.IsArray)
                            {
                                array = (Array)filterPropertyValue;
                            }
                            else
                            {
                                array = new object[1];
                                ((ICollection)filterPropertyValue).CopyTo(array, 0);
                            }

                            firstItem = array.GetValue(0);
                            if (firstItem != null)
                            {
                                var underlyingType = Nullable.GetUnderlyingType(
                                    TypeSystem.GetElementType(validValueProperty.PropertyType)) ?? validValueProperty.PropertyType;

                                queryableValue = queryableValue.Where(validValuePropertyName, underlyingType, firstItem);
                            }
                        }
                        
                        if (firstItem == null)
                            queryableValue = queryableValue.Contains(validValuePropertyName, (IEnumerable)filterPropertyValue);
                    }
                    else if (filterProperty.PropertyType.IsGenericType &&
                        typeof(IRange<>).IsAssignableFrom(filterProperty.PropertyType.GetGenericTypeDefinition()) ||
                        filterProperty.PropertyType.GetInterfaces()
                        .Where(x => x.IsGenericType)
                        .Any(x => x.GetGenericTypeDefinition() == typeof(IRange<>)))
                    {
                        var genericTypeArgument = filterPropertyValue.GetType().GenericTypeArguments.First();

                        if (genericTypeArgument == typeof(byte))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<byte>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(char))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<char>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(DateTime))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<DateTime>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(DateTimeOffset))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<DateTimeOffset>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(decimal))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<decimal>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(double))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<double>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(float))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<float>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(int))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<int>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(long))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<long>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(sbyte))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<sbyte>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(short))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<short>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(uint))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<uint>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(ulong))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<ulong>)filterPropertyValue);
                        }
                        else if (genericTypeArgument == typeof(ushort))
                        {
                            queryableValue = queryableValue.Range(validValuePropertyName, (IRange<ushort>)filterPropertyValue);
                        }
                    }
                    else
                    {
                        try
                        {
                            var propertyType = Nullable.GetUnderlyingType(validValueProperty.PropertyType) ??
                                               validValueProperty.PropertyType;

                            queryableValue = queryableValue.Where(validValuePropertyName, propertyType, filterPropertyValue);
                        }
                        catch (Exception) { }
                    }
                }
            }

            return queryableValue;
        }
        
        private static IQueryable<T> Contains<T>(
            this IQueryable<T> query,
            string property,
            IEnumerable values)
        {
            if (values == null || !values.Cast<object>().Any())
            {
                return query;
            }

            var parameterExpression = Expression.Parameter(typeof(T), "x");
            var propertyExpression = (Expression)Expression.Property(parameterExpression, property);
            var constantExpression = Expression.Constant(values);
            var propertyExpressionIsNullable = propertyExpression.Type.IsGenericType
                                                && propertyExpression.Type.GetGenericTypeDefinition() == typeof(Nullable<>)
                                                && constantExpression.Type.GetElementType() != propertyExpression.Type;

            if (propertyExpressionIsNullable)
            {
                propertyExpression = Expression.Property(propertyExpression, "Value");
            }

            Expression callExpression = Expression.Call(
                typeof(Enumerable),
                "Contains",
                new[] { propertyExpression.Type },
                constantExpression,
                propertyExpression);

            if (propertyExpressionIsNullable)
            {
                var nullablePropertyExpression = Expression.Property(parameterExpression, property);
                var notEqual = Expression.NotEqual(nullablePropertyExpression, Expression.Constant(null, nullablePropertyExpression.Type));
                callExpression = Expression.AndAlso(notEqual, callExpression);
            }

            var lambda = Expression.Lambda<Func<T, bool>>(callExpression, parameterExpression);

            return query.Where(lambda);
        }

        private static IQueryable<T> Where<T>(
            this IQueryable<T> query,
            string property,
            Type valueType,
            object value)
        {
            var parameterExpression = Expression.Parameter(typeof(T), "x");
            var propertyExpression = Expression.Property(parameterExpression, property);

            var method = typeof(ExpressionHelper).GetMethod("WrappedConstant");
            var genericMethod = method.MakeGenericMethod(valueType);

            var constantExpression = (MemberExpression)genericMethod.Invoke(null, new[] {value });
            var comparandExpression = Nullable.GetUnderlyingType(propertyExpression.Type) != null
                ? Expression.Convert(constantExpression, propertyExpression.Type)
                : (Expression)constantExpression;

            var equalExpression = Expression.Equal(propertyExpression, comparandExpression);
            var lambdaExpression = Expression.Lambda<Func<T, bool>>(equalExpression, parameterExpression);
            var callExpression = Expression.Call(
                typeof(Queryable),
                "Where",
                new[] { query.ElementType },
                query.Expression,
                lambdaExpression);

            return query.Provider.CreateQuery<T>(callExpression);
        }

        private static IQueryable<T> Range<T, TRange>(
            this IQueryable<T> query,
            string property,
            IRange<TRange> range)
            where TRange : struct
        {
            var parameterExpression = Expression.Parameter(typeof(T), "x");
            var propertyExpression = Expression.Property(parameterExpression, property);

            Expression minConstantExpression = null;

            if (range.MinValue.HasValue)
            {
                var minValueExpression = Expression.Constant(range.MinValue);
                minConstantExpression = minValueExpression.Type != propertyExpression.Type
                    ? Expression.Convert(minValueExpression, propertyExpression.Type)
                    : (Expression)minValueExpression;
            }

            BinaryExpression minGreaterExpression = null;

            if (minConstantExpression != null)
            {
                minGreaterExpression = range.IsMinInclusive
                ? Expression.GreaterThanOrEqual(propertyExpression, minConstantExpression)
                : Expression.GreaterThan(propertyExpression, minConstantExpression);
            }

            Expression maxConstantExpression = null;

            if (range.MaxValue.HasValue)
            {
                var maxValueExpression = Expression.Constant(range.MaxValue);
                maxConstantExpression = maxValueExpression.Type != propertyExpression.Type
                    ? Expression.Convert(maxValueExpression, propertyExpression.Type)
                    : (Expression)maxValueExpression;
            }

            BinaryExpression maxLessExpression = null;

            if (maxConstantExpression != null)
            {
                maxLessExpression = range.IsMaxInclusive
                ? Expression.LessThanOrEqual(propertyExpression, maxConstantExpression)
                : Expression.LessThan(propertyExpression, maxConstantExpression);
            }

            Expression logicExpression;

            if (minGreaterExpression != null && maxLessExpression != null)
            {
                logicExpression = Expression.And(minGreaterExpression, maxLessExpression);
            }
            else if (minGreaterExpression != null)
            {
                logicExpression = minGreaterExpression;
            }
            else
            {
                logicExpression = maxLessExpression;
            }

            var lambdaExpression = Expression.Lambda<Func<T, bool>>(logicExpression, parameterExpression);
            var callExpression = Expression.Call(
                typeof(Queryable),
                "Where",
                new[] { query.ElementType },
                query.Expression,
                lambdaExpression);

            return query.Provider.CreateQuery<T>(callExpression);
        }

        /**
         * The guy who wrote LINQ wrote the code below. LINQ seems to be doing ok, so I trust it.
         * http://blogs.msdn.com/b/mattwar/archive/2007/07/30/linq-building-an-iqueryable-provider-part-i.aspx
         */
        private static class TypeSystem
        {
            internal static Type GetElementType(Type seqType)
            {
                var ienum = FindIEnumerable(seqType);
                return ienum == null ? seqType : ienum.GetGenericArguments()[0];
            }

            private static Type FindIEnumerable(Type seqType)
            {
                if (seqType == null || seqType == typeof(string))
                    return null;

                if (seqType.IsArray)
                    return typeof(IEnumerable<>).MakeGenericType(seqType.GetElementType());

                if (seqType.IsGenericType)
                {
                    foreach (var arg in seqType.GetGenericArguments())
                    {
                        var ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                        if (ienum.IsAssignableFrom(seqType))
                        {
                            return ienum;
                        }
                    }
                }

                var ifaces = seqType.GetInterfaces();
                if (ifaces.Length > 0)
                {
                    foreach (var iface in ifaces)
                    {
                        var ienum = FindIEnumerable(iface);
                        if (ienum != null) return ienum;
                    }
                }

                if (seqType.BaseType != null && seqType.BaseType != typeof(object))
                {
                    return FindIEnumerable(seqType.BaseType);
                }

                return null;
            }
        }

        /**
         * Source: http://graemehill.ca/entity-framework-dynamic-queries-and-parameterization/
         **/
        private static class ExpressionHelper
        {
            [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "This method is invoked dynamically.")]
            public static MemberExpression WrappedConstant<TValue>(TValue value)
            {
                var wrapper = new WrappedObj<TValue>(value);
                return Expression.Field(
                    Expression.Constant(wrapper),
                    typeof(WrappedObj<TValue>).GetField("Value"));
            }

            [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
            private struct WrappedObj<TValue>
            {
                [SuppressMessage("ReSharper", "NotAccessedField.Local", Justification = "This member is dynamically accessed.")]
                public readonly TValue Value;

                public WrappedObj(TValue value)
                {
                    Value = value;
                }
            }
        }
    }
}
