using System;
using System.Data.SQLite;
using System.IO;
using FluentValidation;
using Nancy;
using Nancy.Extensions;
using Nancy.IO;
using NLog;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Exceptions;
using Radarr.Http.Exceptions;
using Radarr.Http.Extensions;
using HttpStatusCode = Nancy.HttpStatusCode;

namespace Radarr.Http.ErrorManagement
{
    public class RadarrErrorPipeline
    {
        private readonly Logger _logger;

        public RadarrErrorPipeline(Logger logger)
        {
            _logger = logger;
        }

        public Response HandleException(NancyContext context, Exception exception)
        {
            _logger.Trace("Handling Exception");

            if (exception is ApiException apiException)
            {
                _logger.Warn(apiException, "API Error:\n{0}", apiException.Message);
                var body = RequestStream.FromStream(context.Request.Body).AsString();
                _logger.Trace("Request body:\n{0}", body);

                return apiException.ToErrorResponse(context);
            }

            if (exception is ValidationException validationException)
            {
                _logger.Warn("Invalid request {0}", validationException.Message);

                return validationException.Errors.AsResponse(context, HttpStatusCode.BadRequest);
            }

            if (exception is NzbDroneClientException clientException)
            {
                return new ErrorModel
                {
                    Message = exception.Message,
                    Description = exception.ToString()
                }.AsResponse(context, (HttpStatusCode)clientException.StatusCode);
            }

            if (exception is ModelNotFoundException notFoundException)
            {
                return new ErrorModel
                {
                    Message = exception.Message,
                    Description = exception.ToString()
                }.AsResponse(context, HttpStatusCode.NotFound);
            }

            if (exception is ModelConflictException conflictException)
            {
                return new ErrorModel
                {
                    Message = exception.Message,
                    Description = exception.ToString()
                }.AsResponse(context, HttpStatusCode.Conflict);
            }

            if (exception is SQLiteException sqLiteException)
            {
                if (context.Request.Method == "PUT" || context.Request.Method == "POST")
                {
                    if (sqLiteException.Message.Contains("constraint failed"))
                    {
                        return new ErrorModel
                        {
                            Message = exception.Message,
                        }.AsResponse(context, HttpStatusCode.Conflict);
                    }
                }

                _logger.Error(sqLiteException, "[{0} {1}]", context.Request.Method, context.Request.Path);
            }

            _logger.Fatal(exception, "Request Failed. {0} {1}", context.Request.Method, context.Request.Path);

            return new ErrorModel
            {
                Message = exception.Message,
                Description = exception.ToString()
            }.AsResponse(context, HttpStatusCode.InternalServerError);
        }
    }
}
