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

namespace net.r_eg.vsSBE.Events
{
    /// <summary>
    /// Processing with streaming tools
    /// </summary>
    public interface IModeInterpreter
    {
        /// <summary>
        /// Command for handling
        /// </summary>
        string Command { get; set; }

        /// <summary>
        /// Stream handler
        /// </summary>
        string Handler { get; set; }

        /// <summary>
        /// Treat newline as
        /// </summary>
        string Newline { get; set; }

        /// <summary>
        /// Symbol/s for wrapping of commands
        /// </summary>
        string Wrapper { get; set; }
    }
}
