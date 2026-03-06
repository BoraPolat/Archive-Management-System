using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SoftwareDesign
{
    public partial class MultiSelectFieldsPopup : ContentPage
    {
        private ObservableCollection<SelectableField> _fields;
        public Action<List<string>> OnFieldsSelected { get; set; }

        public MultiSelectFieldsPopup(List<string> availableFields, List<string> selectedFields)
        {
            InitializeComponent();

            _fields = new ObservableCollection<SelectableField>(
                availableFields.Select(f => new SelectableField
                {
                    FieldName = f,
                    IsSelected = selectedFields.Contains(f)
                })
            );

            FieldsCollectionView.ItemsSource = _fields;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var selected = _fields.Where(f => f.IsSelected).Select(f => f.FieldName).ToList();
            OnFieldsSelected?.Invoke(selected);
            await Navigation.PopModalAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }

    public class SelectableField : BindableObject
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string FieldName { get; set; }
    }
}