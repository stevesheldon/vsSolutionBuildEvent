﻿/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2013-2015  Denis Kuzmin (reg) <entry.reg@gmail.com>
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
*/

using System;
using System.Runtime.InteropServices;

namespace net.r_eg.vsSBE.Provider
{
    [Guid("53EA79D6-2ACB-43F3-B2A4-C185ABF0A806")]
    public interface IService
    {
        /// <summary>
        /// Gets the DTE2 object instance from project file.
        /// </summary>
        /// <param name="file">Project file. Full path to the *.csproj, *.vcxproj, etc.</param>
        /// <returns></returns>
        EnvDTE80.DTE2 dte2FromProject(string file);

        /// <summary>
        /// Gets the DTE2 object instance from solution file.
        /// </summary>
        /// <param name="file">Project file. Full path to the *.sln</param>
        /// <returns></returns>
        EnvDTE80.DTE2 dte2FromSolution(string file);
    }
}
