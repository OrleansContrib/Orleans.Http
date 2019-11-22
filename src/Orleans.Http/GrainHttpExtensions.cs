using System.Collections.Generic;
using System.Net;

namespace Orleans
{
    public static class GrainHttpExtensions
    {
        public static IGrainHttpResult<TResult> Ok<TResult>(this Grain grain, Dictionary<string, string> responseHeaders) => Ok<TResult>(grain, default(TResult), responseHeaders);
        public static IGrainHttpResult<TResult> Ok<TResult>(this Grain grain, TResult body) => Ok(grain, body, null);
        public static IGrainHttpResult<TResult> Ok<TResult>(this Grain grain) => Ok<TResult>(grain, default(TResult), null);
        public static IGrainHttpResult<TResult> Ok<TResult>(this Grain grain, TResult body, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<TResult> { Body = body, ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.OK };
        }

        public static IGrainHttpResult<TResult> Created<TResult>(this Grain grain, Dictionary<string, string> responseHeaders) => Created<TResult>(grain, default(TResult), responseHeaders);
        public static IGrainHttpResult<TResult> Created<TResult>(this Grain grain, TResult body) => Created(grain, body, null);
        public static IGrainHttpResult<TResult> Created<TResult>(this Grain grain) => Created<TResult>(grain, default(TResult), null);
        public static IGrainHttpResult<TResult> Created<TResult>(this Grain grain, TResult body, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<TResult> { Body = body, ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.Created };
        }

        public static IGrainHttpResult<TResult> Accepted<TResult>(this Grain grain, Dictionary<string, string> responseHeaders) => Accepted<TResult>(grain, default(TResult), responseHeaders);
        public static IGrainHttpResult<TResult> Accepted<TResult>(this Grain grain, TResult body) => Accepted(grain, body, null);
        public static IGrainHttpResult<TResult> Accepted<TResult>(this Grain grain) => Accepted<TResult>(grain, default(TResult), null);
        public static IGrainHttpResult<TResult> Accepted<TResult>(this Grain grain, TResult body, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<TResult> { Body = body, ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.Accepted };
        }

        public static IGrainHttpResult<TResult> Conflict<TResult>(this Grain grain, Dictionary<string, string> responseHeaders) => Conflict<TResult>(grain, default(TResult), responseHeaders);
        public static IGrainHttpResult<TResult> Conflict<TResult>(this Grain grain, TResult body) => Conflict(grain, body, null);
        public static IGrainHttpResult<TResult> Conflict<TResult>(this Grain grain) => Conflict<TResult>(grain, default(TResult), null);
        public static IGrainHttpResult<TResult> Conflict<TResult>(this Grain grain, TResult body, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<TResult> { Body = body, ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.Conflict };
        }

        public static IGrainHttpResult<TResult> BadRequest<TResult>(this Grain grain, Dictionary<string, string> responseHeaders) => BadRequest<TResult>(grain, default(TResult), responseHeaders);
        public static IGrainHttpResult<TResult> BadRequest<TResult>(this Grain grain, TResult body) => BadRequest(grain, body, null);
        public static IGrainHttpResult<TResult> BadRequest<TResult>(this Grain grain) => BadRequest<TResult>(grain, default(TResult), null);
        public static IGrainHttpResult<TResult> BadRequest<TResult>(this Grain grain, TResult body, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<TResult> { Body = body, ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.BadRequest };
        }

        public static IGrainHttpResult<TResult> Unauthorized<TResult>(this Grain grain, Dictionary<string, string> responseHeaders) => Unauthorized<TResult>(grain, default(TResult), responseHeaders);
        public static IGrainHttpResult<TResult> Unauthorized<TResult>(this Grain grain, TResult body) => Unauthorized(grain, body, null);
        public static IGrainHttpResult<TResult> Unauthorized<TResult>(this Grain grain) => Unauthorized<TResult>(grain, default(TResult), null);
        public static IGrainHttpResult<TResult> Unauthorized<TResult>(this Grain grain, TResult body, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<TResult> { Body = body, ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.Unauthorized };
        }

        public static IGrainHttpResult<TResult> Forbidden<TResult>(this Grain grain, Dictionary<string, string> responseHeaders) => Forbidden<TResult>(grain, default(TResult), responseHeaders);
        public static IGrainHttpResult<TResult> Forbidden<TResult>(this Grain grain, TResult body) => Forbidden(grain, body, null);
        public static IGrainHttpResult<TResult> Forbidden<TResult>(this Grain grain) => Forbidden<TResult>(grain, default(TResult), null);
        public static IGrainHttpResult<TResult> Forbidden<TResult>(this Grain grain, TResult body, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<TResult> { Body = body, ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.Forbidden };
        }

        public static IGrainHttpResult NotFound(this Grain grain) => NotFound(grain, null);
        public static IGrainHttpResult NotFound(this Grain grain, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<object> { ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.NotFound };
        }

        public static IGrainHttpResult NoContent(this Grain grain) => NoContent(grain, null);
        public static IGrainHttpResult NoContent(this Grain grain, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<object> { ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.NoContent };
        }

        public static IGrainHttpResult NotAcceptable(this Grain grain) => NotAcceptable(grain, null);
        public static IGrainHttpResult NotAcceptable(this Grain grain, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<object> { ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.NotAcceptable };
        }

        public static IGrainHttpResult NotImplemented(this Grain grain) => NotImplemented(grain, null);
        public static IGrainHttpResult NotImplemented(this Grain grain, Dictionary<string, string> responseHeaders)
        {
            return new GrainHttpResult<object> { ResponseHeaders = responseHeaders, StatusCode = HttpStatusCode.NotImplemented };
        }
    }

    public interface IGrainHttpResult
    {
        Dictionary<string, string> ResponseHeaders { get; }
        HttpStatusCode StatusCode { get; }
        object Body { get; }
    }

    public interface IGrainHttpResult<TResult> : IGrainHttpResult { }

    internal class GrainHttpResult<TResult> : IGrainHttpResult<TResult>
    {
        public Dictionary<string, string> ResponseHeaders { get; set; }

        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        public object Body { get; set; }
    }
}