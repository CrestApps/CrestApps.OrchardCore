/*
 * Draws the charts of a report document. Each chart is a <canvas data-report-chart="{json}"> rendered by the shared
 * report partial. Every chart on the page is drawn when the page loads; content added later (such as the report
 * designer preview) calls window.CrestAppsReportCharts.render(container).
 *
 * A chart people can click carries the keys of its categories (keys) or of its series (seriesKeys): a click raises a
 * report-chart-select event with the key clicked, and window.CrestAppsReportCharts.select(canvas, keys) dims what isn't
 * picked (see report-interactions.js).
 *
 * A chart may carry reference lines ({ label, value, datasetIndex }): each is drawn as a dashed line across the plot at
 * its value, in the color of its dataset, with its label at the end.
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

    // The color of a reference line: that of the dataset it was computed from, or the text color.
    function referenceLineColor(line, textColor) {
        return typeof line.datasetIndex === 'number' ? palette[line.datasetIndex % palette.length] : textColor;
    }

    // The range the value axis is suggested to span so that every reference line shows, besides the values.
    function referenceLineRange(lines, percentageScale) {
        var values = (lines || []).map(function (line) {
            return Number(line.value);
        }).filter(function (value) {
            return isFinite(value);
        });
        var max = percentageScale ? 100 : undefined;
        var min;

        values.forEach(function (value) {
            max = max === undefined ? value : Math.max(max, value);
            min = value < 0 ? (min === undefined ? value : Math.min(min, value)) : min;
        });

        return { suggestedMin: min, suggestedMax: max };
    }

    // Writes the label of a reference line on a faint backing of the page color, so it reads over bars and areas.
    function drawLineLabel(ctx, text, x, y, color, background) {
        var measured = typeof ctx.measureText === 'function' ? ctx.measureText(text) : null;
        var width = measured && measured.width ? measured.width : 0;

        if (width && background) {
            var left = ctx.textAlign === 'right' ? x - width : x;
            var top = ctx.textBaseline === 'top' ? y : y - 13;

            ctx.globalAlpha = 0.85;
            ctx.fillStyle = background;
            ctx.fillRect(left - 3, top - 1, width + 6, 15);
            ctx.globalAlpha = 1;
        }

        ctx.fillStyle = color;
        ctx.fillText(text, x, y);
    }

    // Draws the reference lines of a chart across its plot area, after its datasets, so they stay on top.
    var referenceLinesPlugin = {
        id: 'reportReferenceLines',
        afterDatasetsDraw: function (chart, args, options) {
            var lines = (options && options.lines) || [];
            var area = chart.chartArea;
            var scale = options && options.horizontal ? chart.scales.x : chart.scales.y;
            var ctx = chart.ctx;

            if (!lines.length || !area || !scale) {
                return;
            }

            ctx.save();
            ctx.font = '600 11px system-ui, -apple-system, "Segoe UI", sans-serif';
            ctx.lineWidth = 1.5;

            lines.forEach(function (line) {
                var at = scale.getPixelForValue(Number(line.value));
                var color = referenceLineColor(line, options.textColor || '#495057');

                if (!isFinite(at)) {
                    return;
                }

                ctx.strokeStyle = color;
                ctx.fillStyle = color;
                ctx.setLineDash([6, 4]);
                ctx.beginPath();

                if (options.horizontal) {
                    ctx.moveTo(at, area.top);
                    ctx.lineTo(at, area.bottom);
                    ctx.stroke();
                    ctx.setLineDash([]);
                    ctx.textAlign = at > area.left + ((area.right - area.left) * 0.7) ? 'right' : 'left';
                    ctx.textBaseline = 'top';
                    drawLineLabel(ctx, line.label || '', ctx.textAlign === 'right' ? at - 4 : at + 4, area.top + 2, color, options.background);
                } else {
                    ctx.moveTo(area.left, at);
                    ctx.lineTo(area.right, at);
                    ctx.stroke();
                    ctx.setLineDash([]);
                    // The label ends at the right of the plot, or starts at its left when it is wider than the plot.
                    var wide = typeof ctx.measureText === 'function' && ctx.measureText(line.label || '').width > area.right - area.left - 8;

                    ctx.textAlign = wide ? 'left' : 'right';
                    ctx.textBaseline = at - 14 < area.top ? 'top' : 'bottom';
                    drawLineLabel(ctx, line.label || '', wide ? area.left + 4 : area.right - 4, ctx.textBaseline === 'top' ? at + 3 : at - 3, color, options.background);
                }
            });

            ctx.restore();
        }
    };

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

    function buildConfig(model, textColor, gridColor, background) {
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
        var lines = kind.circular ? [] : (model.referenceLines || []);
        var range = referenceLineRange(lines, model.percentageScale);
        var valueAxis = {
            beginAtZero: true,
            stacked: !!model.stacked,
            suggestedMin: range.suggestedMin,
            suggestedMax: range.suggestedMax,
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
            plugins: lines.length ? [referenceLinesPlugin] : [],
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: kind.horizontal ? 'y' : 'x',
                interaction: {
                    intersect: false,
                    mode: kind.circular ? 'nearest' : 'index'
                },
                plugins: {
                    reportReferenceLines: {
                        lines: lines,
                        horizontal: kind.horizontal,
                        textColor: textColor,
                        background: background || '#ffffff'
                    },
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
        var background = styles.getPropertyValue('--bs-body-bg').trim() || '#ffffff';

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
            canvas.reportChart = new root.Chart(canvas, buildConfig(model, textColor, gridColor, background));
        });
    }

    root.CrestAppsReportCharts = {
        buildConfig: buildConfig,
        chartKind: chartKind,
        keyAt: keyAt,
        referenceLineColor: referenceLineColor,
        referenceLineRange: referenceLineRange,
        referenceLinesPlugin: referenceLinesPlugin,
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
