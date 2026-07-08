using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Helpers
{
    public class EmailAddress
    {
        public string Email { get; private set; }
        public string EmailDomain { get; private set; }

        // Constructor to initialize the EmailAddress object
        // DO napisania i urzytkowania w przyszłości:
        // przy tworzeniu przypisywanie EmailDomain = Email.Split('@')[1]; // Extract the domain from the email address

    }
}
