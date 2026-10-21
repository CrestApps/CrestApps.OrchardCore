/*
 * Draws the charts of a report document. Each chart is a <canvas data-report-chart="{json}"> rendered by the shared
 * report partial. Every chart on the page is drawn when the page loads; content added later (such as the report
 * designer preview) calls window.CrestAppsReportCharts.render(container).
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

    function buildConfig(model, textColor, gridColor) {
        var kind = chartKind(model.type);
        var datasets = (model.datasets || []).map(function (dataset, index) {
            var color = palette[index % palette.length];

            return {
                label: dataset.label,
                data: dataset.data,
                backgroundColor: kind.circular ? palette.slice(0, (dataset.data || []).length) : (kind.fill ? color + '55' : color),
                borderColor: kind.circular ? '#ffffff' : color,
                borderWidth: 2,
                borderRadius: kind.type === 'bar' ? 6 : 0,
                fill: kind.fill,
                tension: kind.type === 'line' ? 0.3 : 0
            };
        });
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
                scales: kind.circular ? {} : (kind.horizontal ? { x: valueAxis, y: categoryAxis } : { x: categoryAxis, y: valueAxis })
            }
        };
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
            new root.Chart(canvas, buildConfig(model, textColor, gridColor));
        });
    }

    root.CrestAppsReportCharts = {
        buildConfig: buildConfig,
        chartKind: chartKind,
        render: render
    };

    if (root.document) {
        if (root.document.readyState === 'complete') {
            render();
        } else {
            root.addEventListener('load', function () { render(); }, { once: true });
        }
    }
})(typeof window !== 'undefined' ? window : globalThis);
