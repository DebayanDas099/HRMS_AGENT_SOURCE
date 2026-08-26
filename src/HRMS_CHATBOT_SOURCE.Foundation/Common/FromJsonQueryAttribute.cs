using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Foundation.Common
{
    public class FromJsonQueryAttribute : ModelBinderAttribute
    {
        public FromJsonQueryAttribute()
        {
            BinderType = typeof(JsonQueryBinder);
        }
    }

    public class JsonQueryBinder : IModelBinder
    {
        private readonly ILogger<JsonQueryBinder> _logger;
        private readonly IConfiguration _config;

        public JsonQueryBinder(ILogger<JsonQueryBinder> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var value = bindingContext.ValueProvider.GetValue(bindingContext.FieldName).FirstValue;
            if (value == null)
            {
                return Task.CompletedTask;
            }

            try
            {
                value = Encryption.Decrypt(value, Convert.ToString(_config.GetSection("AppSettings").GetValue(typeof(string), "EncryptionKey")));
                var parsed = JsonConvert.DeserializeObject(value, bindingContext.ModelType);
                bindingContext.Result = ModelBindingResult.Success(parsed);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to bind '{FieldName}'", bindingContext.FieldName);
                bindingContext.Result = ModelBindingResult.Failed();
            }

            return Task.CompletedTask;
        }
    }
}
