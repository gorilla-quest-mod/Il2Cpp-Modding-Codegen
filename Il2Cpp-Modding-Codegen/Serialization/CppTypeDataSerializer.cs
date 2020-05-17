﻿using Il2Cpp_Modding_Codegen.Data;
using Il2Cpp_Modding_Codegen.Serialization.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Il2Cpp_Modding_Codegen.Serialization
{
    public class CppTypeDataSerializer : ISerializer<ITypeData>
    {
        private string _prefix;
        private bool _asHeader;

        private string _typeName;
        private string _parentName;
        private CppFieldSerializer fieldSerializer;
        private CppMethodSerializer methodSerializer;

        public CppTypeDataSerializer(string prefix = "", bool asHeader = true)
        {
            _prefix = prefix;
            _asHeader = asHeader;
        }

        public void PreSerialize(ISerializerContext context, ITypeData type)
        {
            if (_asHeader)
            {
                _typeName = context.GetNameFromReference(type.This, ForceAsType.Literal);
                if (type.Parent != null)
                    _parentName = context.GetNameFromReference(type.Parent, ForceAsType.Literal);
                // TODO: Make prefix configurable
                fieldSerializer = new CppFieldSerializer(_prefix + "  ");
                foreach (var f in type.Fields)
                    fieldSerializer.PreSerialize(context, f);
            }
            methodSerializer = new CppMethodSerializer(_prefix, _asHeader);
            foreach (var m in type.Methods)
                methodSerializer.PreSerialize(context, m);
        }

        // Should be provided a file, with all references resolved:
        // That means that everything is either forward declared or included (with included files "to be built")
        // This is the responsibility of our parent serializer, who is responsible for converting the context into that
        public void Serialize(Stream stream, ITypeData type)
        {
            // Don't use a using statement here because it will close the underlying stream-- we want to keep it open
            var writer = new StreamWriter(stream);
            // If this type is an interface and a header, write a typedef copy of it
            // Otherwise, ignore it.
            if (type.Type == TypeEnum.Interface)
            {
                if (_asHeader)
                {
                    var specifiers = "";
                    foreach (var spec in type.Specifiers)
                        specifiers += spec + " ";
                    writer.WriteLine($"{_prefix}// Autogenerated interface: {specifiers + type.This}");
                    writer.WriteLine($"{_prefix}typedef struct {_typeName} : Il2CppObject");
                    writer.WriteLine(_prefix + "{");
                    writer.WriteLine(_prefix + "} " + _typeName + ";");
                }
                writer.Flush();
                return;
            }
            if (_asHeader)
            {
                var fieldStream = new MemoryStream();
                foreach (var f in type.Fields)
                {
                    try
                    {
                        fieldSerializer.Serialize(fieldStream, f);
                    }
                    catch (UnresolvedTypeException e)
                    {
                        // Stop serializing the type
                        // TODO: Log the exception
                        var fs = new StreamWriter(fieldStream);
                        fs.WriteLine("/*");
                        fs.WriteLine(e);
                        fs.WriteLine("*/");
                        fs.Flush();
                    }
                }
                var specifiers = "";
                foreach (var spec in type.Specifiers)
                    specifiers += spec + " ";
                writer.WriteLine($"{_prefix}// Autogenerated type: {specifiers + type.This}");
                if (type.ImplementingInterfaces.Count > 0)
                {
                    writer.Write($"{_prefix}// Implementing Interfaces: ");
                    for (int i = 0; i < type.ImplementingInterfaces.Count; i++)
                    {
                        writer.Write(type.ImplementingInterfaces[i]);
                        if (i != type.ImplementingInterfaces.Count - 1)
                            writer.Write(", ");
                    }
                    writer.WriteLine();
                }
                string s = "";
                if (_parentName != null)
                    s = $" : {_parentName}";
                writer.WriteLine($"{_prefix}typedef struct {_typeName + s}");
                writer.WriteLine(_prefix + "{");
                writer.Flush();
                fieldStream.WriteTo(writer.BaseStream);
                writer.WriteLine(_prefix + "} " + _typeName + ";");
                writer.Flush();
                // After the typedef is closed, we write the methods
                // We don't have literal instance methods, they are free functions in a namespace
            }
            foreach (var m in type.Methods)
            {
                try
                {
                    methodSerializer.Serialize(writer.BaseStream, m);
                }
                catch (UnresolvedTypeException e)
                {
                    // Skip the method.
                    // TODO: Log the exception
                    // TODO: Do more with this later
                    writer.WriteLine("/*");
                    writer.WriteLine(e);
                    writer.WriteLine("*/");
                    writer.Flush();
                }
            }
            writer.Flush();
        }
    }
}