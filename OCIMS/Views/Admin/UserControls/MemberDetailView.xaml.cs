using System;
using System.Windows.Controls;
using eSureHi.Models;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class MemberDetailView : UserControl
    {
        public MemberDetailView(Beneficiary member, Action? backAction = null)
        {
            InitializeComponent();
            DataContext = new MemberDetailViewModel(member, backAction);
        }
    }
}
