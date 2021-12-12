// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Dragablz;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Ra2CsfToolsGUI
{
    public class TabablzControlEx : TabablzControl
    {
        static TabablzControlEx() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TabablzControlEx), new FrameworkPropertyMetadata(typeof(TabablzControlEx)));

        public override void OnApplyTemplate()
        {
            if (this._itemsPresenter != null)
            {
                this._itemsPresenter.SizeChanged -= this.OnItemsPresenterSizeChanged;
                this._itemsPresenter = null;
            }

            base.OnApplyTemplate();

            this._rightContentPresenter = this.GetTemplateChild("RightContentPresenter") as ContentPresenter;

            this._leftContentColumn = this.GetTemplateChild("LeftContentColumn") as ColumnDefinition;
            this._tabColumn = this.GetTemplateChild("TabColumn") as ColumnDefinition;
            this._addButtonColumn = this.GetTemplateChild("AddButtonColumn") as ColumnDefinition;
            this._rightContentColumn = this.GetTemplateChild("RightContentColumn") as ColumnDefinition;

            this._tabContainerGrid = this.GetTemplateChild("TabContainerGrid") as Grid;

            this._itemsControl = this.GetTemplateChild(HeaderItemsControlPartName) as DragablzItemsControlEx;
            if (this._itemsControl != null)
            {
                _ = this._itemsControl.ApplyTemplate();

                this._itemsPresenter = this._itemsControl.Template?.FindName("TabsItemsPresenter", this._itemsControl) as ItemsPresenter;
                if (this._itemsPresenter != null)
                {
                    this._itemsPresenter.SizeChanged += this.OnItemsPresenterSizeChanged;
                }
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (this._previousAvailableSize.Width != constraint.Width)
            {
                this._previousAvailableSize = constraint;
                this.UpdateTabWidths();
            }

            return base.MeasureOverride(constraint);
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);

            this.UpdateTabWidths();
        }

        private void OnItemsPresenterSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateTabWidths();
            this._itemsControl?.UpdateScrollViewerDecreaseAndIncreaseButtonsViewState();
        }

        private void UpdateTabWidths()
        {
            if (this._tabContainerGrid != null)
            {
                // Add up width taken by custom content and + button
                double widthTaken = 0.0;
                if (this._leftContentColumn != null)
                {
                    widthTaken += this._leftContentColumn.ActualWidth;
                }
                if (this._addButtonColumn != null)
                {
                    widthTaken += this._addButtonColumn.ActualWidth;
                }
                if (this._rightContentColumn != null)
                {
                    if (this._rightContentPresenter != null)
                    {
                        var rightContentSize = this._rightContentPresenter.DesiredSize;
                        this._rightContentPresenter.MinWidth = rightContentSize.Width;
                        widthTaken += rightContentSize.Width;
                    }
                }

                if (this._tabColumn != null)
                {
                    // Note: can be infinite
                    double availableWidth = this._previousAvailableSize.Width - widthTaken;

                    // Size can be 0 when window is first created; in that case, skip calculations; we'll get a new size soon
                    if (availableWidth > 0)
                    {
                        this._tabColumn.MaxWidth = availableWidth;
                        this._tabColumn.Width = GridLength.Auto;
                        if (this._itemsControl != null)
                        {
                            this._itemsControl.MaxWidth = availableWidth;

                            // Calculate if the scroll buttons should be visible.
                            if (this._itemsPresenter != null)
                            {
                                bool visible = this._itemsPresenter.ActualWidth > availableWidth;
                                ScrollViewer.SetHorizontalScrollBarVisibility(this._itemsControl, visible
                                    ? ScrollBarVisibility.Visible
                                    : ScrollBarVisibility.Hidden);
                                if (visible)
                                {
                                    this._itemsControl.UpdateScrollViewerDecreaseAndIncreaseButtonsViewState();
                                }
                            }
                        }
                    }
                }
            }
        }

        private ColumnDefinition _leftContentColumn;
        private ColumnDefinition _tabColumn;
        private ColumnDefinition _addButtonColumn;
        private ColumnDefinition _rightContentColumn;

        private DragablzItemsControlEx _itemsControl;
        private ContentPresenter _rightContentPresenter;
        private Grid _tabContainerGrid;
        private ItemsPresenter _itemsPresenter;

        private Size _previousAvailableSize;
    }
}
