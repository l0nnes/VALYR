using System;

namespace Core.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ConfigHeaderAttribute : Attribute
    {
        public ConfigHeaderAttribute(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ConfigTooltipAttribute : Attribute
    {
        public ConfigTooltipAttribute(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ConfigRangeAttribute : Attribute
    {
        public ConfigRangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Min { get; }
        public float Max { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ConfigSpaceAttribute : Attribute
    {
        public ConfigSpaceAttribute(float height = 10f)
        {
            Height = height;
        }

        public float Height { get; }
    }
}