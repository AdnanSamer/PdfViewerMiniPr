using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;

namespace PdfViewerMiniPr.ModelBinders;

public class DictionaryStringStringModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }

        var result = new Dictionary<string, string>();

        try
        {
            // Read the request body
            var request = bindingContext.HttpContext.Request;
            if (!request.Body.CanSeek)
            {
                request.EnableBuffering();
            }

            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var jsonString = reader.ReadToEnd();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(jsonString))
            {
                bindingContext.Result = ModelBindingResult.Success(result);
                return Task.CompletedTask;
            }

            // Parse JSON and convert all values to strings
            var jsonObject = JObject.Parse(jsonString);
            foreach (var prop in jsonObject.Properties())
            {
                var token = prop.Value;
                string value;

                if (token == null || token.Type == JTokenType.Null)
                {
                    value = string.Empty;
                }
                else if (token.Type == JTokenType.Boolean)
                {
                    // Convert boolean to lowercase string (true/false)
                    value = token.ToObject<bool>().ToString().ToLowerInvariant();
                }
                else if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                {
                    value = token.ToString();
                }
                else
                {
                    value = token.ToString();
                }

                result[prop.Name] = value;
            }

            bindingContext.Result = ModelBindingResult.Success(result);
        }
        catch (Exception ex)
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Error binding model: {ex.Message}");
            bindingContext.Result = ModelBindingResult.Success(result); // Return empty dict on error
        }

        return Task.CompletedTask;
    }
}

