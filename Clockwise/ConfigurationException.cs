using System;

namespace Clockwise
{
    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message, Exception exception) : base(message, exception)
        {
        }
    }
}