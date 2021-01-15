using System;

namespace GentleTouch
{
    [AttributeUsage(AttributeTargets.Method)]
    public class DoNotShowInHelpAttribute : Attribute
    {
    }
}