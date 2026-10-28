/*
 * Draws the charts of a report document. Each chart is a <canvas data-report-chart="{json}"> rendered by the shared
 * report partial. Every chart on the page is drawn when the page loads; content added later (such as the report
 * designer preview) calls window.CrestAppsReportCharts.render(container).
 *
 * A chart people can click carries the keys of its categories (keys) or of its series (seriesKeys): a click raises a
 * report-chart-select event with the key clicked, and window.CrestAppsReportCharts.select(canvas, keys) dims what isn't
 * picked (see report-interactions.js).
 */
(function (root) {
    'use strict';

    var palette = ['#2563eb', '#14b8a6', '#f59e0b', '#ef4444', '#8b5cf6', '#06b6d4', '#84cc16', '#f97316', '#ec4899', '#64748b'];

    // Maps a report chart type to the Chart.js type and the options that set it apart.
    function chartKind(type) {
        switch (type) {
            case 'area':
                return { type: 'line', fill: true, horizontal: false, circular: false };
            case 'horizontalbar':
                return { type: 'bar', fill: false, horizontal: true, circular: false };
            case 'pie':
                return { type: 'pie', fill: false, horizontal: false, circular: true };
            case 'doughnut':
                return { type: 'doughnut', fill: false, horizontal: false, circular: true };
            case 'line':
                return { type: 'line', fill: false, horizontal: false, circular: false };
            default:
                return { type: 'bar', fill: false, horizontal: false, circular: false };
        }
    }

    // A palette color made faint, for what a pick leaves out.
    function faint(color) {
        return color.length === 7 ? color + '33' : color;
    }

    // The colors of a dataset with the picked keys: the categories or series picked keep their color and the others
    // fade; with nothing picked, every mark keeps its color.
    function colorsOf(model, kind, index, count, keys) {
        var color = palette[index % palette.length];
        var picked = keys || [];
        var categoryKeys = model.keys || null;
        var seriesKey = model.seriesKeys ? model.seriesKeys[index] : undefined;
        var fadedSeries = picked.length > 0 && seriesKey !== undefined && picked.indexOf(seriesKey) < 0;
        var marks = [];

        for (var position = 0; position < count; position++) {
            var base = kind.circular ? palette[position % palette.length] : (kind.fill ? color + '55' : color);
            var fadedCategory = picked.length > 0 && categoryKeys && picked.indexOf(categoryKeys[position]) < 0;

            marks.push(fadedSeries || fadedCategory ? faint(kind.circular ? palette[position % palette.length] : color) : base);
        }

        return {
            background: kind.circular || (picked.length && categoryKeys) ? marks : (fadedSeries ? faint(color) : (kind.fill ? color + '55' : color)),
            border: kind.circular ? '#ffffff' : (fadedSeries ? faint(color) : color),
            points: picked.length && categoryKeys ? marks : color
        };
    }

    function applySelection(datasets, model, kind, keys) {
        datasets.forEach(function (dataset, index) {
            var colors = colorsOf(model, kind, index, (dataset.data || []).length, keys);

            dataset.backgroundColor = colors.background;
            dataset.borderColor = colors.border;

            if (kind.type === 'line') {
                dataset.pointBackgroundColor = colors.points;
                dataset.pointBorderColor = colors.points;
            }
        });
    }

    // The key of the category or series a click landed on, or undefined when the chart picks nothing there.
    function keyAt(model, hit) {
        if (!hit) {
            return undefined;
        }

        if (model.keys) {
            return model.keys[hit.index];
        }

        return model.seriesKeys ? model.seriesKeys[hit.datasetIndex] : undefined;
    }

    function buildConfig(model, textColor, gridColor) {
        var kind = chartKind(model.type);
        var selectable = !!(model.keys || model.seriesKeys);
        var datasets = (model.datasets || []).map(function (dataset) {
            return {
                label: dataset.label,
                data: dataset.data,
                borderWidth: 2,
                borderRadius: kind.type === 'bar' ? 6 : 0,
                fill: kind.fill,
                tension: kind.type === 'line' ? 0.3 : 0
            };
        });

        applySelection(datasets, model, kind, model.selectedKeys);
        var valueAxis = {
            beginAtZero: true,
            stacked: !!model.stacked,
            suggestedMax: model.percentageScale ? 100 : undefined,
            grid: { color: gridColor },
            ticks: {
                color: textColor,
                callback: model.percentageScale ? function (value) { return value + '%'; } : undefined
            }
        };
        var categoryAxis = {
            stacked: !!model.stacked,
            grid: { display: false },
            ticks: { color: textColor }
        };

        return {
            type: kind.type,
            data: {
                labels: model.labels || [],
                datasets: datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: kind.horizontal ? 'y' : 'x',
                interaction: {
                    intersect: false,
                    mode: kind.circular ? 'nearest' : 'index'
                },
                plugins: {
                    legend: {
                        display: model.showLegend !== false,
                        position: 'bottom',
                        labels: {
                            color: textColor,
                            usePointStyle: true,
                            padding: 18
                        }
                    }
                },
                scales: kind.circular ? {} : (kind.horizontal ? { x: valueAxis, y: categoryAxis } : { x: categoryAxis, y: valueAxis }),
                onHover: selectable ? function (event, elements, chart) {
                    chart.canvas.style.cursor = elements.length ? 'pointer' : 'default';
                } : undefined,
                onClick: selectable ? function (event, elements, chart) {
                    // The mark under the pointer, rather than every series at that category.
                    var exact = chart.getElementsAtEventForMode(event.native || event, 'nearest', { intersect: true }, false);
                    var key = keyAt(model, exact[0] || elements[0]);

                    if (key === undefined) {
                        return;
                    }

                    chart.canvas.dispatchEvent(new root.CustomEvent('report-chart-select', {
                        bubbles: true,
                        detail: { key: key, additive: !!(event.native && (event.native.ctrlKey || event.native.metaKey)) }
                    }));
                } : undefined
            }
        };
    }

    // Dims what a chart's picked keys leave out.
    function select(canvas, keys) {
        var chart = canvas && canvas.reportChart;
        var model = canvas && canvas.reportChartModel;

        if (!chart || !model) {
            return;
        }

        model.selectedKeys = keys || [];
        applySelection(chart.data.datasets, model, chartKind(model.type), model.selectedKeys);
        chart.update();
    }

    function render(container) {
        if (typeof root.Chart === 'undefined' || !root.document) {
            return;
        }

        var scope = container || root.document;
        var styles = root.getComputedStyle(root.document.documentElement);
        var textColor = styles.getPropertyValue('--bs-body-color').trim() || '#495057';
        var gridColor = styles.getPropertyValue('--bs-border-color').trim() || 'rgba(0, 0, 0, 0.1)';

        scope.querySelectorAll('canvas[data-report-chart]').forEach(function (canvas) {
            if (canvas.dataset.reportChartRendered === 'true') {
                return;
            }

            var model;

            try {
                model = JSON.parse(canvas.dataset.reportChart);
            } catch (e) {
                return;
            }

            canvas.dataset.reportChartRendered = 'true';
            canvas.reportChartModel = model;
            canvas.reportChart = new root.Chart(canvas, buildConfig(model, textColor, gridColor));
        });
    }

    root.CrestAppsReportCharts = {
        buildConfig: buildConfig,
        chartKind: chartKind,
        keyAt: keyAt,
        render: render,
        select: select
    };

    if (root.document) {
        if (root.document.readyState === 'complete') {
            render();
        } else {
            root.addEventListener('load', function () { render(); }, { once: true });
        }
    }
})(typeof window !== 'undefined' ? window : globalThis);
