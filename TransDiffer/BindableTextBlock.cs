using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace TransDiffer
{
    public class BindableTextBlock : TextBlock
    {
        public static readonly DependencyProperty PartsSourceProperty = DependencyProperty.Register(
            "PartsSource", typeof(IEnumerable<Inline>), typeof(BindableTextBlock), new PropertyMetadata(default(IEnumerable<Inline>)));

        public IEnumerable<Inline> PartsSource
        {
            get { return (IEnumerable<Inline>)GetValue(PartsSourceProperty); }
            set { SetValue(PartsSourceProperty, value); }
        }

        public BindableTextBlock()
        {
            var dpd = DependencyPropertyDescriptor.FromProperty(PartsSourceProperty, typeof(BindableTextBlock));
            dpd?.AddValueChanged(this, OnPartsSourceChanged);
        }

        private void OnPartsSourceChanged(object sender, EventArgs eventArgs)
        {
            Inlines.Clear();
            Inlines.AddRange(PartsSource);
            // TODO: Handle ObservableCollection binding if provided
        }
    }
}
