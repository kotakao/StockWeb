// Lightweight Charts v4.2.3 的 JS Interop 包裝，供 LightweightChart.razor 呼叫。
// 依賴全域 LightweightCharts（standalone 版由 App.razor 以 <script> 載入）。
// render/dispose 皆冪等：重複 render 會先清掉舊圖表再重建。

export function render(element, spec) {
    dispose(element);

    const chart = LightweightCharts.createChart(element, {
        height: spec.height,
        layout: { background: { color: 'transparent' }, textColor: '#333', fontSize: 12 },
        grid: { vertLines: { color: '#f0f0f0' }, horzLines: { color: '#f0f0f0' } },
        rightPriceScale: { visible: true, borderVisible: false },
        leftPriceScale: { visible: !!spec.leftScale, borderVisible: false },
        timeScale: { borderVisible: false },
        localization: { locale: 'zh-TW' },
    });

    for (const s of spec.series) {
        const options = {};
        if (s.color) options.color = s.color;
        if (s.priceScaleId) options.priceScaleId = s.priceScaleId;
        if (s.title) options.title = s.title;

        let series;
        switch (s.type) {
            case 'candlestick':
                // 台灣慣例：紅漲綠跌。
                series = chart.addCandlestickSeries({
                    priceScaleId: s.priceScaleId || 'right',
                    upColor: '#d32f2f', downColor: '#2e7d32',
                    borderUpColor: '#d32f2f', borderDownColor: '#2e7d32',
                    wickUpColor: '#d32f2f', wickDownColor: '#2e7d32',
                });
                series.setData(s.candles);
                continue;
            case 'histogram': series = chart.addHistogramSeries(options); break;
            case 'area': series = chart.addAreaSeries(options); break;
            default: series = chart.addLineSeries(options); break;
        }
        // 逐點 color 為 null 時移除，讓 Lightweight 沿用序列預設色。
        series.setData(s.data.map(p => (p.color ? p : { time: p.time, value: p.value })));
    }

    chart.timeScale().fitContent();
    element._chart = chart;

    // 容器寬度變動時同步重繪（RWD）。
    element._resizeObserver = new ResizeObserver(entries => {
        for (const entry of entries) chart.applyOptions({ width: entry.contentRect.width });
    });
    element._resizeObserver.observe(element);
}

export function dispose(element) {
    if (element._resizeObserver) { element._resizeObserver.disconnect(); element._resizeObserver = null; }
    if (element._chart) { element._chart.remove(); element._chart = null; }
}
