using APTS_WEB_APP.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;

namespace APTS_WEB_APP.Filters
{
    public class ServerInputValidationFilterAttribute : ActionFilterAttribute
    {
        private const int MaxStringLength = 2000;

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                return;
            }

            bool jsonRequest = IsJsonRequest(filterContext);
            List<string> errors = new List<string>();
            HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

            foreach (KeyValuePair<string, object> argument in filterContext.ActionParameters)
            {
                ValidateValue(argument.Value, argument.Key, errors, visited, 0, jsonRequest);
            }

            if (jsonRequest)
            {
                foreach (ModelState modelState in filterContext.Controller.ViewData.ModelState.Values)
                {
                    foreach (ModelError modelError in modelState.Errors)
                    {
                        string message = modelError.ErrorMessage;
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            errors.Add(message);
                        }
                    }
                }
            }

            if (!errors.Any())
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            string errorMessage = errors.First();
            if (jsonRequest)
            {
                filterContext.Result = new JsonResult
                {
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    Data = new
                    {
                        success = false,
                        isValid = false,
                        message = errorMessage
                    }
                };
                return;
            }

            filterContext.Result = new HttpStatusCodeResult(400, errorMessage);
        }

        private static void ValidateValue(object value, string fieldName, IList<string> errors, ISet<object> visited, int depth, bool includeModelValidation)
        {
            if (value == null || depth > 5)
            {
                return;
            }

            string stringValue = value as string;
            if (stringValue != null)
            {
                ValidateString(stringValue, fieldName, errors);
                return;
            }

            Type valueType = value.GetType();
            if (IsSimpleType(valueType))
            {
                return;
            }

            if (!visited.Add(value))
            {
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int index = 0;
                foreach (object item in enumerable)
                {
                    ValidateValue(item, fieldName + "[" + index + "]", errors, visited, depth + 1, includeModelValidation);
                    index++;
                }
                return;
            }

            if (includeModelValidation)
            {
                ValidationContext context = new ValidationContext(value, null, null);
                List<ValidationResult> validationResults = new List<ValidationResult>();
                Validator.TryValidateObject(value, context, validationResults, true);
                foreach (ValidationResult validationResult in validationResults)
                {
                    if (!string.IsNullOrWhiteSpace(validationResult.ErrorMessage))
                    {
                        errors.Add(validationResult.ErrorMessage);
                    }
                }
            }

            foreach (System.Reflection.PropertyInfo property in valueType.GetProperties())
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                ValidateValue(property.GetValue(value, null), fieldName + "." + property.Name, errors, visited, depth + 1, includeModelValidation);
            }
        }

        private static void ValidateString(string value, string fieldName, IList<string> errors)
        {
            if (value.Length > MaxStringLength)
            {
                errors.Add(fieldName + " exceeds allowed length.");
                return;
            }

            if (ContainsBlockedControlCharacter(value))
            {
                errors.Add(fieldName + " contains invalid characters.");
                return;
            }

            if (HtmlInjectionGuard.ContainsHtmlInjection(value))
            {
                errors.Add(fieldName + " contains unsafe HTML/script input.");
            }
        }

        private static bool ContainsBlockedControlCharacter(string value)
        {
            foreach (char character in value)
            {
                if (char.IsControl(character) && character != '\r' && character != '\n' && character != '\t')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSimpleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(Guid);
        }

        private static bool IsJsonRequest(ActionExecutingContext filterContext)
        {
            string contentType = filterContext.HttpContext.Request.ContentType ?? string.Empty;
            bool acceptsJson = filterContext.HttpContext.Request.AcceptTypes != null
                && filterContext.HttpContext.Request.AcceptTypes.Any(type => type.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0);

            return filterContext.HttpContext.Request.IsAjaxRequest()
                || contentType.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0
                || acceptsJson;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
