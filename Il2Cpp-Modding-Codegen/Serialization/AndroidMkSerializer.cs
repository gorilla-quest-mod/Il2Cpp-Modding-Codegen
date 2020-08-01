﻿using Il2CppModdingCodegen.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Il2CppModdingCodegen.Serialization
{
    /// <summary>
    /// Serializes an Android.mk file
    /// </summary>
    public sealed class AndroidMkSerializer : System.IDisposable
    {
        internal class Library
        {
            internal IEnumerable<string> toBuild;
            internal bool isSource;
            internal string id;
            public Library(string _id, bool _isSource, IEnumerable<string> _toBuild)
            {
                id = _id;
                isSource = _isSource;
                toBuild = _toBuild;
            }
        }

        private const string HeaderString = @"# Copyright (C) 2009 The Android Open Source Project
#
# Licensed under the Apache License, Version 2.0 (the License);
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an AS IS BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
#
#
# Autogenerated by codegen
LOCAL_PATH := $(call my-dir)
TARGET_ARCH_ABI := $(APP_ABI)
rwildcard=$(wildcard $1$2) $(foreach d,$(wildcard $1*),$(call rwildcard,$d/,$2))
";

        private TextWriter? _stream;
        private readonly SerializationConfig _config;

        internal AndroidMkSerializer(SerializationConfig config)
        {
            _config = config;
        }

        internal void WriteHeader(string filename)
        {
            if (File.Exists(filename))
                File.Delete(filename);
            _stream = new StreamWriter(File.OpenWrite(filename));
            _stream.WriteLine(HeaderString);
        }

        /// <summary>
        /// Writes a static library, containing either source or library files
        /// </summary>
        /// <param name="contexts"></param>
        internal void WriteStaticLibrary(Library lib)
        {
            if (_stream is null) throw new InvalidOperationException("Must call WriteHeader first!");
            _stream.WriteLine("# Writing static library: " + lib.id);
            _stream.WriteLine("include $(CLEAR_VARS)");
            _stream.WriteLine($"LOCAL_MODULE := {lib.id}");
            var prefix = lib.isSource ? "LOCAL_SRC_FILES" : "LOCAL_STATIC_LIBRARIES";
            foreach (var item in lib.toBuild)
                _stream.WriteLine(prefix + " += " + item);
            _stream.WriteLine("LOCAL_C_INCLUDES := ./include ./src");
            _stream.WriteLine($"LOCAL_CFLAGS += -DMOD_ID='\"{_config.Id}\"' -DVERSION='\"{_config.Version}\"' -DNEED_UNSAFE_CSHARP");
            _stream.WriteLine($"LOCAL_CFLAGS += -I'{_config.Libil2cpp}' -I'./extern/beatsaber-hook/shared'");
            _stream.WriteLine("include $(BUILD_STATIC_LIBRARY)");
            _stream.WriteLine("");
        }

        internal void WritePrebuiltSharedLibrary(string id, string src, string include)
        {
            if (_stream is null) throw new InvalidOperationException("Must call WriteHeader first!");
            _stream.WriteLine("# Writing prebuilt shared library: " + id);
            _stream.WriteLine("include $(CLEAR_VARS)");
            _stream.WriteLine($"LOCAL_MODULE := {id}");
            _stream.WriteLine("LOCAL_SRC_FILES := " + src);
            _stream.WriteLine("LOCAL_CPP_FEATURES := rtti" + (_config.OutputStyle == OutputStyle.ThrowUnless ? " exceptions" : ""));
            _stream.WriteLine("LOCAL_EXPORT_C_INCLUDES := " + include);
            _stream.WriteLine("include $(PREBUILT_SHARED_LIBRARY)");
            _stream.WriteLine("");
        }

        internal void WriteSharedLibrary(Library lib)
        {
            if (_stream is null) throw new InvalidOperationException("Must call WriteHeader first!");
            _stream.WriteLine("# Writing shared library: " + lib.id);
            _stream.WriteLine("include $(CLEAR_VARS)");
            _stream.WriteLine($"LOCAL_MODULE := {lib.id}");
            // Write bs-hook SRC. This should be substituted for linking with the bs-hook.so
            _stream.WriteLine("LOCAL_SRC_FILES := $(call rwildcard,extern/beatsaber-hook/shared/inline-hook/,*.cpp) $(call rwildcard,extern/beatsaber-hook/shared/utils/,*.cpp) $(call rwildcard,extern/beatsaber-hook/shared/inline-hook/,*.c)");
            _stream.WriteLine("LOCAL_C_INCLUDES := ./include ./src");
            _stream.WriteLine($"LOCAL_CFLAGS += -DMOD_ID='\"{_config.Id}\"' -DVERSION='\"{_config.Version}\"' -DNEED_UNSAFE_CSHARP");
            _stream.WriteLine($"LOCAL_CFLAGS += -I'{_config.Libil2cpp}' -I'./extern/beatsaber-hook/shared'");
            var prefix = lib.isSource ? "LOCAL_SRC_FILES" : "LOCAL_STATIC_LIBRARIES";
            foreach (var item in lib.toBuild)
                _stream.WriteLine(prefix + " += " + item);
            _stream.WriteLine("LOCAL_LDLIBS := -llog");
            _stream.WriteLine("include $(BUILD_SHARED_LIBRARY)");
            _stream.WriteLine("");
        }

        internal void WriteSingleFile(Library lib)
        {
            if (_stream is null) throw new InvalidOperationException("Must call WriteHeader first!");
            _stream.WriteLine("# Writing single library: " + lib.id);
            _stream.WriteLine("include $(CLEAR_VARS)");
            _stream.WriteLine($"LOCAL_MODULE := {lib.id}");
            _stream.WriteLine("LOCAL_SRC_FILES += $(call rwildcard,./src,*.cpp)");
            _stream.WriteLine("LOCAL_C_INCLUDES := ./include ./src");
            _stream.WriteLine($"LOCAL_CFLAGS += -DMOD_ID='\"{_config.Id}\"' -DVERSION='\"{_config.Version}\"' -DNEED_UNSAFE_CSHARP");
            _stream.WriteLine($"LOCAL_CFLAGS += -I'{_config.Libil2cpp}' -Wno-inaccessible-base -DNO_CODEGEN_USE");
            foreach (var l in lib.toBuild)
                _stream.WriteLine("LOCAL_SHARED_LIBRARIES += " + l);
            _stream.WriteLine("LOCAL_LDLIBS := -llog");
            _stream.WriteLine("include $(BUILD_SHARED_LIBRARY)");
        }

        private static int aggregateIdx = 0;

        internal void AggregateStaticLibraries(IEnumerable<Library> libs, int depth = 0)
        {
            if (_config.Id is null) throw new InvalidOperationException("config.Id should have a value!");
            int innerLibsLength = 0;
            int outterLibsLength = 0;
            var innerLibs = new List<Library>();
            var outterLibs = new List<Library>();
            var innerNamesOnly = new List<string>();
            int idx = 0;
            // Iterate over each static library, add them to a partial.
            foreach (var l in libs)
            {
                if (innerLibsLength + l.id.Length >= _config.StaticLibraryCharacterLimit)
                {
                    // If the library we are attempting to add would put us over limit, add all the old ones to a shared library
                    var newLib = new Library(_config.Id + "_partial_" + depth + "_" + idx, false, innerNamesOnly);
                    WriteStaticLibrary(newLib);
                    // Reset inners
                    innerLibsLength = 0;
                    innerLibs.Clear();
                    innerNamesOnly.Clear();
                    // Add new lib to outters
                    outterLibs.Add(newLib);
                    outterLibsLength += newLib.id.Length;
                    idx++;
                }
                innerLibs.Add(l);
                innerLibsLength += l.id.Length;
                innerNamesOnly.Add(l.id);
            }
            // If I have made exactly 0 outter libraries, I may as well just write the innerLibraries instead.
            if (outterLibsLength == 0)
            {
                outterLibs = innerLibs;
                outterLibsLength = innerLibsLength;
                // Actually, this could cause some issues since we could recurse forever... (given that SharedLibraryCharacterLimit and StaticLibraryCharacterLimit are not equal)
                // This should be changed to split into 2 if such a case happens. As it stands, it'll recurse forever in such a situation
            }
            // Upon reaching the end, we should have a list of static libraries that have been created wrapping the provided static libraries.
            if (outterLibsLength >= _config.SharedLibraryCharacterLimit)
                AggregateStaticLibraries(outterLibs, depth + 1);
            else
            {
                // Otherwise, simply write out the outterLibs directly, and be done!
                var overall = new Library(_config.Id + "_complete_" + depth + "_" + aggregateIdx, false, outterLibs.Select(l => l.id));
                if (depth != 0)
                {
                    WriteStaticLibrary(overall);
                    aggregateIdx++;
                }
                else
                {
                    // If we have completed depth = 0, that means we have written everything!
                    overall.id = _config.Id;
                    WriteSharedLibrary(overall);
                }
            }
            // If we find that this is too long, recurse until we are small enough
        }

        public void Close() => _stream?.Close();
        public void Dispose() => Close();
    }
}
