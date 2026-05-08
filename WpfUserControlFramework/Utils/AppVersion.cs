using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;

namespace RestaurantPosWpf
{
    internal class AppVersion
    {
        private string sVerMajor = "", sVerMinor = "", sBuild = "", sVerRevision = "", sVerDate = "";

        private void SetVersionInfo()
        {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version ?? new Version(0, 0, 0, 0);
            sVerMajor = ver.Major.ToString();
            sVerMinor = ver.Minor.ToString();
            sBuild = ver.Build.ToString();
            sVerRevision = ver.Revision.ToString();

            // Legacy code referenced a now-removed Environ.AppVersionDate helper. Fall back to the
            // executing assembly's file timestamp, which is a reasonable "build date" surrogate and
            // does not depend on any external static class.
            try
            {
                var loc = asm.Location;
                sVerDate = string.IsNullOrEmpty(loc)
                    ? DateTime.Now.ToString("yyyy-MM-dd")
                    : File.GetLastWriteTime(loc).ToString("yyyy-MM-dd");
            }
            catch
            {
                sVerDate = DateTime.Now.ToString("yyyy-MM-dd");
            }
        }

        internal string FullVersion
        {
            get
            {
                if (sVerMajor == "") SetVersionInfo();
                return sVerMajor + "." + sVerMinor + "." + sBuild + "." + sVerRevision;
            }
        }

        internal string VerMajor
        {
            get
            {
                if (sVerMajor == "") SetVersionInfo();
                return sVerMajor;
            }
        }

        internal string VerMinor
        {
            get
            {
                if (sVerMinor == "") SetVersionInfo();
                return sVerMinor;
            }
        }

        internal string VerBuild
        {
            get
            {
                if (sBuild == "") SetVersionInfo();
                return sBuild;
            }
        }

        internal string VerRevision
        {
            get
            {
                if (sVerRevision == "") SetVersionInfo();
                return sVerRevision;
            }
        }

        internal string VerDate
        {
            get
            {
                if (sVerDate == "") SetVersionInfo();
                return sVerDate;
            }
        }
    }
}
