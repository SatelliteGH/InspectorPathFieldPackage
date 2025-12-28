using UnityEngine;
using System;

namespace InspectorPathField
{
    public class PathFieldAttribute : PropertyAttribute
    {
        public readonly Type DefaultSerchType;

        public PathFieldAttribute(Type defaultSerchType)
        {
            DefaultSerchType = defaultSerchType;
        }
    }
}