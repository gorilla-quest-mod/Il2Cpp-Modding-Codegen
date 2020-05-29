﻿using Il2Cpp_Modding_Codegen.Parsers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Il2Cpp_Modding_Codegen.Data.DllHandling
{
    internal class DllImage : IImage
    {
        public string Name { get; }
        public int Start { get; }

        public DllImage(PeekableStreamReader fs)
        {
            var line = fs.ReadLine();
            var split = line.Split(' ');
            if (split.Length < 6)
            {
                throw new InvalidOperationException($"Could not create Image out of: \"{line.Trim()}\"");
            }
            Start = int.Parse(split[split.Length - 1]);
            Name = split[split.Length - 3];
        }

        public override string ToString()
        {
            return $"{Name} - {Start}";
        }
    }
}