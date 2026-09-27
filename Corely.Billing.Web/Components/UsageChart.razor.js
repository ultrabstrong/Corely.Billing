const charts = new Map();
let chartJs;
let themeObserver;

function watchTheme() {
    themeObserver ??= new MutationObserver(() => {
        for (const [canvas, entry] of charts) {
            if (!canvas.isConnected) {
                destroyChart(canvas);
                continue;
            }
            entry.redraw();
        }
    });
    themeObserver.observe(document.documentElement, {
        subtree: true,
        attributes: true,
        attributeFilter: ['data-bs-theme'],
    });
}

function loadChartJs() {
    chartJs ??= import(new URL('../lib/chart.js/chart.umd.min.js', import.meta.url).href)
        .then(() => globalThis.Chart);
    return chartJs;
}

function tokens(canvas) {
    const style = getComputedStyle(canvas.closest('.cbw-chart') ?? canvas);
    const read = name => style.getPropertyValue(name).trim();
    return {
        usage: read('--cbw-series-1'),
        capacity: [read('--cbw-series-2'), read('--cbw-series-3'), read('--cbw-series-4')],
        palette: [1, 2, 3, 4, 5, 6].map(i => read(`--cbw-series-${i}`)),
        other: read('--cbw-series-other'),
        ink: read('--cbw-ink'),
        muted: read('--cbw-ink-muted'),
        grid: read('--cbw-grid'),
        surface: read('--cbw-surface'),
    };
}

const number = new Intl.NumberFormat();

function wash(color) {
    return /^#[0-9a-f]{6}$/i.test(color) ? `${color}1a` : color;
}

function baseOptions(t, { legend, stacked }) {
    return {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 250 },
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: legend
                ? {
                      position: 'top',
                      align: 'start',
                      labels: { color: t.ink, usePointStyle: true, pointStyle: 'rectRounded', boxHeight: 8, padding: 16 },
                  }
                : { display: false },
            tooltip: {
                backgroundColor: t.surface,
                titleColor: t.ink,
                bodyColor: t.ink,
                borderColor: t.grid,
                borderWidth: 1,
                padding: 10,
                usePointStyle: true,
                callbacks: { label: item => ` ${item.dataset.label}: ${number.format(item.parsed.y)}` },
            },
        },
        scales: {
            x: {
                stacked,
                grid: { display: false },
                border: { color: t.grid },
                ticks: { color: t.muted, maxRotation: 0, autoSkipPadding: 16 },
            },
            y: {
                stacked,
                beginAtZero: true,
                grid: { color: t.grid, lineWidth: 1 },
                border: { display: false },
                ticks: { color: t.muted, precision: 0, maxTicksLimit: 5, callback: value => number.format(value) },
            },
        },
    };
}

async function draw(canvas, build) {
    const Chart = await loadChartJs();
    destroyChart(canvas);
    const chart = new Chart(canvas, build(tokens(canvas)));
    charts.set(canvas, { chart, redraw: () => draw(canvas, build) });
    watchTheme();
}

function unitText(value, unit) {
    return `${number.format(value)} ${unit}${value === 1 ? '' : 's'}`;
}

function withUnit(options, unit) {
    options.plugins.tooltip.callbacks.label = item => {
        const value = options.indexAxis === 'y' ? item.parsed.x : item.parsed.y;
        const over = item.dataset.overdrawn?.[item.dataIndex] ?? 0;
        const note = over > 0 ? `, overdrawn by ${unitText(over, unit)}` : '';
        return ` ${item.dataset.label ?? item.label}: ${unitText(value, unit)}${note}`;
    };
    return options;
}

function seriesColor(t, series, i) {
    return series.other ? t.other : t.palette[i % t.palette.length];
}

function bars(t, series, { stacked }) {
    return series.map((s, i) => ({
        label: s.label,
        data: s.data,
        backgroundColor: seriesColor(t, s, i),
        borderRadius: stacked ? 0 : { topLeft: 4, topRight: 4 },
        borderSkipped: 'start',
        maxBarThickness: 24,
        categoryPercentage: 0.9,
        barPercentage: 0.9,
    }));
}

function lines(t, series, { stepped, dashFirst }) {
    return series.map((s, i) => {
        const color = seriesColor(t, s, i);
        return {
            label: s.label,
            data: s.data,
            overdrawn: s.overdrawn,
            stepped: stepped ? 'middle' : false,
            borderColor: color,
            backgroundColor: color,
            borderWidth: i === 0 && dashFirst ? 3 : 2,
            borderDash: [],
            spanGaps: false,
            pointRadius: 0,
            pointHoverRadius: 4,
            pointHoverBorderWidth: 2,
            pointHoverBorderColor: t.surface,
            pointHoverBackgroundColor: color,
            fill: false,
        };
    });
}

function share(t, spec) {
    const options = baseOptions(t, { legend: false, stacked: false });
    options.indexAxis = 'y';
    options.scales.x.grid = { color: t.grid, lineWidth: 1 };
    options.scales.x.beginAtZero = true;
    options.scales.x.ticks.callback = value => number.format(value);
    options.scales.y.grid = { display: false };
    options.scales.y.ticks = { color: t.ink };
    return {
        type: 'bar',
        data: {
            labels: spec.series.map(s => s.label),
            datasets: [{
                label: 'Used',
                data: spec.series.map(s => s.data[0] ?? 0),
                backgroundColor: spec.series.map((s, i) => seriesColor(t, s, i)),
                borderRadius: 4,
                maxBarThickness: 28,
            }],
        },
        options: withUnit(options, spec.unit),
    };
}

export function renderChart(canvas, spec) {
    return draw(canvas, t => {
        switch (spec.kind) {
            case 'share':
                return share(t, spec);
            case 'stacked':
            case 'grouped': {
                const stacked = spec.kind === 'stacked';
                return {
                    type: 'bar',
                    data: { labels: spec.labels, datasets: bars(t, spec.series, { stacked }) },
                    options: withUnit(baseOptions(t, { legend: true, stacked }), spec.unit),
                };
            }
            case 'remaining':
            case 'burnup':
                return {
                    type: 'line',
                    data: {
                        labels: spec.labels,
                        datasets: lines(t, spec.series, {
                            stepped: spec.kind === 'remaining',
                            dashFirst: spec.kind === 'remaining',
                        }).map((d, i) => (spec.kind === 'burnup' && i > 0 ? { ...d, stepped: 'middle' } : d)),
                    },
                    options: withUnit(baseOptions(t, { legend: true, stacked: false }), spec.unit),
                };
            default:
                return {
                    type: 'bar',
                    data: { labels: spec.labels, datasets: bars(t, spec.series, { stacked: false }) },
                    options: withUnit(baseOptions(t, { legend: false, stacked: false }), spec.unit),
                };
        }
    });
}

export function renderCapacity(canvas, labels, series) {
    return draw(canvas, t => ({
        type: 'line',
        data: { labels, datasets: capacityDatasets(t, series) },
        options: baseOptions(t, { legend: true, stacked: true }),
    }));
}

function capacityDatasets(t, series) {
    return series.map((s, i) => {
        const color = s.other ? t.other : t.capacity[i] ?? t.other;
        return {
            label: s.label,
            data: s.data,
            stepped: 'middle',
            borderColor: color,
            backgroundColor: wash(color),
            borderWidth: 2,
            fill: i === 0 ? 'origin' : '-1',
            pointRadius: 0,
            pointHoverRadius: 4,
            pointHoverBorderWidth: 2,
            pointHoverBorderColor: t.surface,
            pointHoverBackgroundColor: color,
        };
    });
}

export function destroyChart(canvas) {
    const entry = canvas && charts.get(canvas);
    if (entry) {
        entry.chart.destroy();
        charts.delete(canvas);
    }
}
