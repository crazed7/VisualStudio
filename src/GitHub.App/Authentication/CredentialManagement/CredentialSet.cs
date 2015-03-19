﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace GitHub.Authentication.CredentialManagement
{
    public class CredentialSet : List<Credential>, IDisposable
    {
        bool _disposed;

        public CredentialSet()
        {
        }

        public CredentialSet(string target)
            : this()
        {
            Guard.ArgumentNotEmptyString(target, "target");

            Target = target;
        }

        public string Target { get; set; }


        public void Dispose()
        {
            Dispose(true);

            // Prevent GC Collection since we have already disposed of this object
            GC.SuppressFinalize(this);
        }

        ~CredentialSet()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (Count > 0)
                    {
                        ForEach(cred => cred.Dispose());
                    }
                }
            }
            _disposed = true;
        }

        public CredentialSet Load()
        {
            LoadInternal();
            return this;
        }

        private void LoadInternal()
        {
            uint count;

            var pCredentials = IntPtr.Zero;
            bool result = NativeMethods.CredEnumerateW(Target, 0, out count, out pCredentials);
            if (!result)
            {
                var lastError = Marshal.GetLastWin32Error();
                Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, "Win32Exception: {0}", new Win32Exception(lastError).ToString()));
                return;
            }

            // Read in all of the pointers first
            var ptrCredList = new IntPtr[count];
            for (int i = 0; i < count; i++)
            {
                ptrCredList[i] = Marshal.ReadIntPtr(pCredentials, IntPtr.Size * i);
            }

            // Now let's go through all of the pointers in the list
            // and create our Credential object(s)
            var credentialHandles =
                ptrCredList.Select(ptrCred => new NativeMethods.CriticalCredentialHandle(ptrCred)).ToList();

            var existingCredentials = credentialHandles
                .Select(handle => handle.GetCredential())
                .Select(nativeCredential =>
                {
                    Credential credential = new Credential();
                    credential.LoadInternal(nativeCredential);
                    return credential;
                });
            AddRange(existingCredentials);

            // The individual credentials should not be free'd
            credentialHandles.ForEach(handle => handle.SetHandleAsInvalid());

            // Clean up memory to the Enumeration pointer
            NativeMethods.CredFree(pCredentials);
        }
    }
}