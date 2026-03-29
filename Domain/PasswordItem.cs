using System;
using System.Collections.Generic;

namespace OnPass.Domain
{
    public class PasswordHistoryEntry
    {
        public string Password { get; set; } = string.Empty;
        public DateTime DateChanged { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
    }

    public class PasswordItem
    {
        public string Name { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }
        public List<PasswordHistoryEntry> PasswordHistory { get; set; }

        public PasswordItem()
        {
            PasswordHistory = new List<PasswordHistoryEntry>();
            DateCreated = DateTime.Now;
            DateModified = DateTime.Now;
        }

        public void UpdatePassword(string newPassword, string changedBy)
        {
            if (!string.IsNullOrEmpty(Password) && Password != newPassword)
            {
                PasswordHistory.Add(new PasswordHistoryEntry
                {
                    Password = Password,
                    DateChanged = DateModified,
                    ChangedBy = changedBy
                });
            }
            Password = newPassword;
            DateModified = DateTime.Now;
        }

        public void RestorePassword(int historyIndex, string changedBy)
        {
            if (historyIndex >= 0 && historyIndex < PasswordHistory.Count)
            {
                var historyEntry = PasswordHistory[historyIndex];

                if (Password != historyEntry.Password)
                {
                    PasswordHistory.Add(new PasswordHistoryEntry
                    {
                        Password = Password,
                        DateChanged = DateModified,
                        ChangedBy = changedBy
                    });
                }

                PasswordHistory.RemoveAt(historyIndex);

                Password = historyEntry.Password;
                DateModified = DateTime.Now;
            }
        }

        public void ClearHistory()
        {
            PasswordHistory.Clear();
        }

        public List<PasswordHistoryEntry> GetPasswordHistory()
        {
            return new List<PasswordHistoryEntry>(PasswordHistory);
        }
    }
}
