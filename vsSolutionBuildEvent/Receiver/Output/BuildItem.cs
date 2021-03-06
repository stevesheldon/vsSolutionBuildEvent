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
using System.Text.RegularExpressions;

namespace net.r_eg.vsSBE.Receiver.Output
{
    public class BuildItem
    {
        public int ErrorsCount
        {
            get { return errors.Count; }
        }

        public int WarningsCount
        {
            get { return warnings.Count; }
        }

        public bool IsErrors
        {
            get { return ErrorsCount > 0; }
        }

        public bool IsWarnings
        {
            get { return WarningsCount > 0; }
        }

        /// <summary>
        /// all errors in partial data
        /// </summary>
        public List<string> Errors
        {
            get { return errors; }
        }
        protected List<string> errors = new List<string>();

        /// <summary>
        /// all warnings in partial data
        /// </summary>
        public List<string> Warnings
        {
            get { return warnings; }
        }
        protected List<string> warnings = new List<string>();

        public enum Type
        {
            Warnings,
            Errors
        }

        /// <summary>
        /// Current raw
        /// </summary>
        public string Raw
        {
            get { 
                return (rawdata == null)? String.Empty : rawdata; 
            }
            set {
                updateRaw(value);
            }
        }
        protected string rawdata;

        /// <summary>
        /// Updating data and immediately extracting contents
        /// </summary>
        /// <param name="rawdata"></param>
        public void updateRaw(string rawdata)
        {
            this.rawdata = rawdata;
            extract();
        }

        public bool checkRule(Type type, bool isWhitelist, List<string> codes)
        {
            if(isWhitelist) {
                if((codes.Count < 1 && (type == Type.Warnings ? Warnings : Errors).Count > 0) || 
                    (codes.Count > 0 && codes.Intersect(type == Type.Warnings ? Warnings : Errors).Count() > 0)) {
                    return true;
                }
                return false;
            }

            if(codes.Count < 1) {
                return false;
            }
            if((type == Type.Warnings ? Warnings : Errors).Except(codes).Count() > 0) {
                return true;
            }
            return false;
        }

        protected void extract()
        {
            flushCodes();
            // Format specification: http://msdn.microsoft.com/en-us/library/yxkt8b26%28v=vs.120%29.aspx
            MatchCollection matches = Regex.Matches(rawdata, @"\s+(error|warning)\s+([^:]+):", RegexOptions.IgnoreCase);
            // 1  -> type
            // 2  -> code####

            foreach(Match m in matches){
                if(!m.Success){
                    continue;
                }

                string code = m.Groups[2].Value.Trim();
                switch(m.Groups[1].Value)
                {
                    case "error": { errors.Add(code); break; }
                    case "warning": { warnings.Add(code); break; }
                }
            }
        }

        protected void flushCodes()
        {
            errors.Clear();
            warnings.Clear();
        }
    }
}
