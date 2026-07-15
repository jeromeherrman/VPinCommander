namespace VPinCommander.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new CabinetsPageViewModel();
    }
}
