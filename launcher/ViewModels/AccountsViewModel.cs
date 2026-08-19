using System.Collections.ObjectModel;
using NarutoLauncher.Models;

namespace NarutoLauncher.ViewModels;

public class AccountsViewModel
{
    public ObservableCollection<Account> Accounts => App.CurrentApp.Accounts.Accounts;
}
