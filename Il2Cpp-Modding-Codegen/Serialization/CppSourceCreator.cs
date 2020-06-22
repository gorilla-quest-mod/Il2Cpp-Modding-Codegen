﻿using Il2Cpp_Modding_Codegen.Config;
using Il2Cpp_Modding_Codegen.Data;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

namespace Il2Cpp_Modding_Codegen.Serialization
{
    public class CppSourceCreator
    {
        private SerializationConfig _config;
        private CppContextSerializer _serializer;

        public CppSourceCreator(SerializationConfig config, CppContextSerializer serializer)
        {
            _config = config;
            _serializer = serializer;
        }

        public void Serialize(CppTypeContext context)
        {
            var data = context.LocalType;
            if (data.Type == TypeEnum.Interface || data.Methods.Count == 0 || data.This.IsGeneric)
            {
                // Don't create C++ for types with no methods, or if it is an interface, or if it is generic, or if context is a header
                return;
            }

            var sourceLocation = Path.Combine(_config.OutputDirectory, _config.OutputSourceDirectory, context.CppFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(sourceLocation));
            using (var ms = new MemoryStream())
            {
                var rawWriter = new StreamWriter(ms);
                var writer = new CppStreamWriter(rawWriter);
                // Write header
                writer.WriteComment($"Autogenerated from {nameof(CppSourceCreator)} on {DateTime.Now}");
                writer.WriteComment($"Created by Sc2ad");
                writer.WriteComment("=========================================================================");
                try
                {
                    // Write SerializerContext and actual type
                    _serializer.Serialize(writer, context, false);
                }
                catch (UnresolvedTypeException e)
                {
                    if (_config.UnresolvedTypeExceptionHandling.TypeHandling == UnresolvedTypeExceptionHandling.DisplayInFile)
                    {
                        writer.WriteLine("// Unresolved type exception!");
                        writer.WriteLine("/*");
                        writer.WriteLine(e);
                        writer.WriteLine("*/");
                    }
                    else if (_config.UnresolvedTypeExceptionHandling.TypeHandling == UnresolvedTypeExceptionHandling.SkipIssue)
                        return;
                    else if (_config.UnresolvedTypeExceptionHandling.TypeHandling == UnresolvedTypeExceptionHandling.Elevate)
                        throw new InvalidOperationException($"Cannot elevate {e} to a parent type- there is no parent type!");
                }
                writer.Flush();
                if (File.Exists(sourceLocation))
                    throw new InvalidOperationException($"Was about to overwrite existing file: {sourceLocation} with context: {context.LocalType.This}");
                using (var fs = File.OpenWrite(sourceLocation))
                {
                    rawWriter.BaseStream.Position = 0;
                    rawWriter.BaseStream.CopyTo(fs);
                }
            }
        }
    }
}