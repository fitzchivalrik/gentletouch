﻿using System;

namespace GentleTouch.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class HelpMessageAttribute : Attribute
    {
        public HelpMessageAttribute(string helpMessage)
        {
            HelpMessage = helpMessage;
        }

        public string HelpMessage { get; }
    }
}