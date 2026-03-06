// LocationGuidePage.xaml.cs dosyası

namespace SoftwareDesign;

public partial class LocationGuidePage : ContentPage
{
	public LocationGuidePage()
	{
		InitializeComponent();
	}

    private async void OnBackClicked(object sender, EventArgs e)
    {
        // MainPage'de "PushModalAsync" kullanıldığı için burada "PopModalAsync" kullanılmalı.
        await Navigation.PopModalAsync();
    }
}