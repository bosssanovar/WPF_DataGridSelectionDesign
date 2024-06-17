using Reactive.Bindings;
using Reactive.Bindings.Extensions;

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using static System.Net.Mime.MediaTypeNames;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow2 : Window
    {
        private const int InitCulumnCount = 100;

        public ObservableCollection<Detail> Items { get; private set; } = new ObservableCollection<Detail>();

        private ScrollSynchronizer? _scrollSynchronizer;

        public ReactivePropertySlim<bool> IsDataGridEnabled { get; } = new(true);

        public MainWindow2()
        {

            InitializeComponent();

            InitData(InitCulumnCount);

            IsDataGridEnabled.Where(x => !x).Subscribe(
                x =>
                {
                    grid.UnselectAllCells();
                });
        }

        #region 画面の初期化

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            Cursor = Cursors.Wait;

            InitColumns(InitCulumnCount);

            grid.Visibility = Visibility.Visible;

            Dispatcher.InvokeAsync(() =>
            {
                Cursor = null;

                InitScrollSynchronizer();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void InitScrollSynchronizer()
        {
            var scrollList = new List<ScrollViewer>();
            var gridScroll = DataGridHelper.GetScrollViewer(grid);
            if (gridScroll is not null)
            {
                scrollList.Add(gridScroll);
            }
            _scrollSynchronizer = new ScrollSynchronizer(scrollList);
        }
        #endregion

        #region 行列の初期化

        private void InitColumns(int count)
        {
            grid.Columns.Clear();

            var converter = new BooleanToVisibilityConverter();
            for (int columnIndex = 0; columnIndex < count; ++columnIndex)
            {
                var binding = new Binding($"Values[{columnIndex}].Value");
                binding.Converter = converter;

                var factory = new FrameworkElementFactory(typeof(Ellipse));
                factory.SetValue(Ellipse.HeightProperty, 15.0);
                factory.SetValue(Ellipse.WidthProperty, 15.0);
                factory.SetValue(Ellipse.FillProperty, new SolidColorBrush(Color.FromRgb(0xe2, 0xaf, 0x42)));
                factory.SetBinding(Ellipse.VisibilityProperty, binding);

                var dataTemplate = new DataTemplate();
                dataTemplate.VisualTree = factory;

                var column = new DataGridTemplateColumn();
                column.CellTemplate = dataTemplate;

                grid.Columns.Add(column);
            }
        }

        private void InitData(int count)
        {
            // バインドを切断
            Binding b = new Binding("Items")
            {
                Source = null
            };
            grid.SetBinding(DataGrid.ItemsSourceProperty, b);

            var list = new List<Detail>();
            for (int i = 0; i < count; i++)
            {
                list.Add(new Detail(InitCulumnCount));
            }

            Items = new ObservableCollection<Detail>(list);

            b = new Binding("Items")
            {
                Source = this
            };
            grid.SetBinding(DataGrid.ItemsSourceProperty, b);
        }
        #endregion

        #region 設定値変更

        private void DataGridCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsDataGridEnabled.Value)
            {
                return;
            }

            if (grid.SelectedCells.Count == 1)
            {
                var columnIndex = DataGridHelper.GetSelectedColumnIndex(grid);
                var rowIndex = DataGridHelper.GetSelectedRowIndex(grid);

                Items[rowIndex].Invert(columnIndex);
            }
        }

        private void DataGridCell_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsDataGridEnabled.Value)
            {
                return;
            }

            if (grid.SelectedCells.Count <= 1)
            {
                grid.Focus();
                grid.SelectedCells.Clear();

                DataGridCell? targetCell = DataGridHelper.GetCellAtMousePosition(sender, e);

                if (targetCell is null) return;
                grid.CurrentCell = new DataGridCellInfo(targetCell);
                grid.SelectedCells.Add(grid.CurrentCell);

                ShowContextMenu(false);
            }
            else
            {
                ShowContextMenu(true);
            }
        }

        private void ShowContextMenu(bool isSelectArea)
        {
            ContextMenu contextMenu = new ContextMenu();

            MenuItem menuItem = new MenuItem();
            menuItem.Header = "行全部設定";
            menuItem.Click += new RoutedEventHandler(AllOn);
            menuItem.IsEnabled = !isSelectArea;
            contextMenu.Items.Add(menuItem);

            Separator separator = new Separator();
            contextMenu.Items.Add(separator);

            menuItem = new MenuItem();
            menuItem.Header = "選択エリア設定";
            menuItem.Click += new RoutedEventHandler(AreaOn);
            menuItem.IsEnabled = isSelectArea;
            contextMenu.Items.Add(menuItem);

            contextMenu.IsOpen = true;
        }

        private void AllOn(object sender, RoutedEventArgs e)
        {
            var rowIndex = DataGridHelper.GetSelectedRowIndex(grid);
            Items[rowIndex].SetAll(true);
        }

        private void AreaOn(object sender, RoutedEventArgs e)
        {
            var indexes = DataGridHelper.GetSelectedCellsIndex(grid);
            foreach (var index in indexes)
            {
                Items[index.RowIndex].SetOn(index.ColumnIndex);
            }
        }

        #endregion

        private FillHandleAdorner? fillHandleAdorner = null;

        private void grid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            var datagrid = (System.Windows.Controls.DataGrid)sender;
            AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(datagrid);
            if (datagrid.SelectedCells.Any())
            {
                DataGridCellInfo firstCellInfo = datagrid.SelectedCells.First();
                FrameworkElement firstElt = firstCellInfo.Column.GetCellContent(firstCellInfo.Item);

                DataGridCellInfo lastCellInfo = datagrid.SelectedCells.Last();
                FrameworkElement lastElt = lastCellInfo.Column.GetCellContent(lastCellInfo.Item);

                if (firstElt != null && lastElt != null)
                {
                    var firstcell = (DataGridCell)firstElt.Parent;
                    var lastCell = (DataGridCell)lastElt.Parent;
                    Point topLeft = datagrid.PointFromScreen(firstcell.PointToScreen(new Point(0, 0)));
                    Point bottomRight = datagrid.PointFromScreen(lastCell.PointToScreen(new Point(lastCell.ActualWidth, lastCell.ActualHeight)));
                    var rect = new Rect(topLeft, bottomRight);
                    if (fillHandleAdorner == null)
                    {
                        fillHandleAdorner = new FillHandleAdorner(datagrid, rect);
                        adornerLayer.Add(fillHandleAdorner);
                    }
                    else
                        fillHandleAdorner.Rect = rect;
                }
            }
            else
            {
                adornerLayer.Remove(fillHandleAdorner);
                fillHandleAdorner = null;
            }
        }
    }

    internal class FillHandleAdorner : Adorner
    {
        Rect rect;

        readonly VisualCollection visualChildren;

        System.Windows.Controls.DataGrid dataGrid;

        public Rect Rect
        {
            get { return rect; }
            set
            {
                rect = value;
                InvalidateVisual();
            }
        }

        public FillHandleAdorner(UIElement adornedElement, Rect rect)
            : base(adornedElement)
        {
            dataGrid = (System.Windows.Controls.DataGrid)adornedElement;
            Rect = rect;

            visualChildren = new VisualCollection(this);
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xec, 0xa1, 0x2a)));
        }

        protected override int VisualChildrenCount
        {
            get { return visualChildren.Count; }
        }

        protected override Visual GetVisualChild(int index)
        {
            return visualChildren[index];
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var blackSolidBrush = new SolidColorBrush(Color.FromRgb(0xec, 0xa1, 0x2a));
            var pen = new Pen(blackSolidBrush, 3);
            pen.Freeze();
            double halfPenWidth = pen.Thickness / 2;

            Rect rangeBorderRect = rect;
            rangeBorderRect.Offset(-1, -1);

            GuidelineSet guidelines = new GuidelineSet();
            guidelines.GuidelinesX.Add(rangeBorderRect.Left + halfPenWidth);
            guidelines.GuidelinesX.Add(rangeBorderRect.Right + halfPenWidth);
            guidelines.GuidelinesY.Add(rangeBorderRect.Top + halfPenWidth);
            guidelines.GuidelinesY.Add(rangeBorderRect.Bottom + halfPenWidth);

            Point p1 = rangeBorderRect.BottomRight;
            p1.Offset(0, +1);
            guidelines.GuidelinesY.Add(p1.Y + halfPenWidth);

            Point p2 = rangeBorderRect.BottomRight;
            p2.Offset(+1, 0);
            guidelines.GuidelinesX.Add(p2.X + halfPenWidth);

            drawingContext.PushGuidelineSet(guidelines);

            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(p1, true, false);
                ctx.LineTo(rangeBorderRect.TopRight, true, false);
                ctx.LineTo(rangeBorderRect.TopLeft, true, false);
                ctx.LineTo(rangeBorderRect.BottomLeft, true, false);
                ctx.LineTo(p2, true, false);
            }
            geometry.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);

            drawingContext.Pop();
        }
    }
}