// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Dragablz;
using ModernWpf.Controls.Primitives;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Ra2CsfToolsGUI
{
    public class DragablzItemsControlEx : DragablzItemsControl
    {
        private const double c_scrollAmount = 50.0;

        static DragablzItemsControlEx() => DefaultStyleKeyProperty.OverrideMetadata(typeof(DragablzItemsControlEx), new FrameworkPropertyMetadata(typeof(DragablzItemsControlEx)));

        public override void OnApplyTemplate()
        {
            if (this._scrollViewer != null)
            {
                this._scrollViewer.ScrollChanged -= this.OnScrollViewerScrollChanged;
            }

            if (this._scrollDecreaseButton != null)
            {
                this._scrollDecreaseButton.IsVisibleChanged -= this.OnScrollButtonIsVisibleChanged;
                this._scrollDecreaseButton.Click -= this.OnScrollDecreaseClick;
            }

            if (this._scrollIncreaseButton != null)
            {
                this._scrollIncreaseButton.IsVisibleChanged -= this.OnScrollButtonIsVisibleChanged;
                this._scrollIncreaseButton.Click -= this.OnScrollIncreaseClick;
            }

            base.OnApplyTemplate();

            this._scrollViewer = this.GetTemplateChild("ScrollViewer") as ScrollViewer;
            if (this._scrollViewer != null)
            {
                _ = this._scrollViewer.ApplyTemplate();
                this._scrollViewer.ScrollChanged += this.OnScrollViewerScrollChanged;

                this._scrollDecreaseButton = this._scrollViewer.Template?.FindName("ScrollDecreaseButton", this._scrollViewer) as RepeatButton;
                if (this._scrollDecreaseButton != null)
                {
                    this._scrollDecreaseButton.IsVisibleChanged += this.OnScrollButtonIsVisibleChanged;
                    this._scrollDecreaseButton.Click += this.OnScrollDecreaseClick;
                }

                this._scrollIncreaseButton = this._scrollViewer.Template?.FindName("ScrollIncreaseButton", this._scrollViewer) as RepeatButton;
                if (this._scrollIncreaseButton != null)
                {
                    this._scrollIncreaseButton.IsVisibleChanged += this.OnScrollButtonIsVisibleChanged;
                    this._scrollIncreaseButton.Click += this.OnScrollIncreaseClick;
                }
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var desiredSize = base.MeasureOverride(constraint);

            if (this.ItemsOrganiser != null)
            {
                var padding = this.Padding;
                double width = this.ItemsPresenterWidth + padding.Left + padding.Right;
                double height = this.ItemsPresenterHeight + padding.Top + padding.Bottom;

                if (this._scrollDecreaseButton != null && this._scrollDecreaseButton.IsVisible)
                {
                    width += this._scrollDecreaseButton.ActualWidth;
                }
                if (this._scrollIncreaseButton != null && this._scrollIncreaseButton.IsVisible)
                {
                    width += this._scrollIncreaseButton.ActualWidth;
                }

                desiredSize.Width = Math.Min(constraint.Width, width);
                desiredSize.Height = Math.Min(constraint.Height, height);
            }

            return desiredSize;
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            if (element is DragablzItem dragablzItem && item is TabItem tabItem)
            {
                _ = dragablzItem.SetBinding(DragablzItemHelper.IconProperty,
                    new Binding { Path = new PropertyPath(TabItemHelper.IconProperty), Source = tabItem });
            }
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);

            if (element is DragablzItem dragablzItem && item is TabItem)
            {
                dragablzItem.ClearValue(DragablzItemHelper.IconProperty);
            }
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            this.InvalidateMeasure();
        }

        private void OnScrollButtonIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                this.InvalidateMeasure();
            }
        }

        private void OnScrollDecreaseClick(object sender, RoutedEventArgs e)
        {
            if (this._scrollViewer != null)
            {
                this._scrollViewer.ScrollToHorizontalOffset(Math.Max(0, this._scrollViewer.HorizontalOffset - c_scrollAmount));
            }
        }

        private void OnScrollIncreaseClick(object sender, RoutedEventArgs e)
        {
            if (this._scrollViewer != null)
            {
                this._scrollViewer.ScrollToHorizontalOffset(Math.Min(this._scrollViewer.ScrollableWidth, this._scrollViewer.HorizontalOffset + c_scrollAmount));
            }
        }

        private void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e) => this.UpdateScrollViewerDecreaseAndIncreaseButtonsViewState();

        internal void UpdateScrollViewerDecreaseAndIncreaseButtonsViewState()
        {
            if (this._scrollViewer != null && this._scrollDecreaseButton != null && this._scrollIncreaseButton != null)
            {
                const double minThreshold = 0.1;
                double horizontalOffset = this._scrollViewer.HorizontalOffset;
                double scrollableWidth = this._scrollViewer.ScrollableWidth;

                if (Math.Abs(horizontalOffset - scrollableWidth) < minThreshold)
                {
                    this._scrollDecreaseButton.IsEnabled = true;
                    this._scrollIncreaseButton.IsEnabled = false;
                }
                else if (Math.Abs(horizontalOffset) < minThreshold)
                {
                    this._scrollDecreaseButton.IsEnabled = false;
                    this._scrollIncreaseButton.IsEnabled = true;
                }
                else
                {
                    this._scrollDecreaseButton.IsEnabled = true;
                    this._scrollIncreaseButton.IsEnabled = true;
                }
            }
        }

        private ScrollViewer _scrollViewer;
        private RepeatButton _scrollDecreaseButton;
        private RepeatButton _scrollIncreaseButton;
    }
}
