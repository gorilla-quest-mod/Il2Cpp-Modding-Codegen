﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Il2Cpp_Modding_Codegen.Serialization
{
    public class ApplicationMkSerializer
    {
        private const string ApplicationMk = @"APP_ABI := arm64-v8a
APP_PLATFORM := 24
APP_PIE := true
APP_STL := c++_static
APP_CFLAGS := -std= gnu18
APP_CPPFLAGS := -std= gnu++2a
APP_SHORT_COMMANDS := true";

        private TextWriter _stream;

        public void Write(string filename)
        {
            if (File.Exists(filename))
                File.Delete(filename);
            _stream = new StreamWriter(File.OpenWrite(filename));
            _stream.WriteLine(ApplicationMk);
        }

        public void Close()
        {
            _stream.Close();
        }
    }
}