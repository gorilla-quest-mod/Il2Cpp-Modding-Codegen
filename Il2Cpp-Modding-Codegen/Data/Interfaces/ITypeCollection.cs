﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Il2Cpp_Modding_Codegen.Data
{
    public interface ITypeCollection
    {
        IEnumerable<ITypeData> Types { get; }
        [ObsoleteAttribute("Please call TypeRef.Resolve(ITypeCollection) instead.")]
        ITypeData Resolve(TypeRef TypeRef);
    }
}