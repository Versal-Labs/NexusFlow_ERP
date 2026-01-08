using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Shared.Wrapper
{
    public class Result<T>
    {
        public T Data { get; set; }
        public bool Succeeded { get; set; }
        public string[] Errors { get; set; }
        public string Message { get; set; }

        // Static Factory Method for Success
        public static Result<T> Success(T data, string message = null)
        {
            return new Result<T>
            {
                Succeeded = true,
                Data = data,
                Message = message,
                Errors = null
            };
        }

        // Static Factory Method for Failure
        public static Result<T> Failure(params string[] errors)
        {
            return new Result<T>
            {
                Succeeded = false,
                Errors = errors
            };
        }

        // Static Factory Method for Failure with Message
        public static Result<T> Failure(string message)
        {
            return new Result<T>
            {
                Succeeded = false,
                Message = message,
                Errors = new[] { message }
            };
        }
    }

    // Non-Generic Version (for Commands that return nothing)
    public class Result
    {
        public bool Succeeded { get; set; }
        public string[] Errors { get; set; }
        public string Message { get; set; }

        public static Result Success(string message = null)
        {
            return new Result
            {
                Succeeded = true,
                Message = message
            };
        }

        public static Result Failure(params string[] errors)
        {
            return new Result
            {
                Succeeded = false,
                Errors = errors
            };
        }
    }
}
