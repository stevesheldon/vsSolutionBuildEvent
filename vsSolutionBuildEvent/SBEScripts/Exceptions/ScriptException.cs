﻿/*
 * Copyright (c) 2013-2014  Denis Kuzmin (reg) <entry.reg@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using net.r_eg.vsSBE.Exceptions;

namespace net.r_eg.vsSBE.SBEScripts.Exceptions
{
    public class ScriptException: SBEException
    {
        public ScriptException(string message): base(message) {}
        public ScriptException(string message, params object[] args): base(message, args) {}
    }

    public class SelectorMismatchException: ScriptException
    {
        public SelectorMismatchException(string message): base(message) {}
        public SelectorMismatchException(string message, params object[] args): base(message, args) {}
    }

    public class SyntaxIncorrectException: ScriptException
    {
        public SyntaxIncorrectException(string message): base(message) {}
        public SyntaxIncorrectException(string message, params object[] args): base(message, args) {}
    }

    public class SubtypeNotFoundException: ScriptException
    {
        public SubtypeNotFoundException(string message): base(message) {}
        public SubtypeNotFoundException(string message, params object[] args): base(message, args) {}
    }

    public class OperandNotFoundException: NotFoundException
    {
        public OperandNotFoundException(string message): base(message) {}
        public OperandNotFoundException(string message, params object[] args): base(message, args) {}
    }

    public class OperationNotFoundException: NotFoundException
    {
        public OperationNotFoundException(string message) : base(message) { }
        public OperationNotFoundException(string message, params object[] args): base(message, args) {}
    }

    public class NotSupportedOperationException: ScriptException
    {
        public NotSupportedOperationException(string message): base(message) {}
        public NotSupportedOperationException(string message, params object[] args): base(message, args) {}
    }
}
