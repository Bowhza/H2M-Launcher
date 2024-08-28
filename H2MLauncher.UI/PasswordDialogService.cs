using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using H2MLauncher.Core.Services;

namespace H2MLauncher.UI
{
    public class PasswordDialogService : IPasswordDialogService
    {
        public string GetPassword()
        {
            PasswordDialog passwordDialog = new PasswordDialog();
            bool? result = passwordDialog.ShowDialog();

            if (result == true)
            {
                return passwordDialog.Password;
            }
            return null; // Or return an empty string or throw an exception if the user cancels
        }
    }

}
