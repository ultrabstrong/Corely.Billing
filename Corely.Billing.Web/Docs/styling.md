# Styling

The components use Bootstrap 5.3 classes and scoped CSS. Every color comes from Bootstrap's CSS variables, so a host's `data-bs-theme="dark"` on any ancestor switches them.

## Chart Colors

The chart reads its colors from custom properties on `.cbw-chart`. The defaults are a validated categorical palette, with a darker set under `[data-bs-theme="dark"]`. The chart redraws when the theme attribute changes.

| Property | Used for |
|----------|----------|
| `--cbw-series-1` | Used, per period |
| `--cbw-series-2` to `--cbw-series-4` | The first three grants' capacity |
| `--cbw-series-other` | Grants folded into "Other grants" |
| `--cbw-ink`, `--cbw-ink-muted` | Legend and axis text |
| `--cbw-grid` | Gridlines |

Override them from a stylesheet loaded after the host's bundle:

```css
.cbw-chart {
    --cbw-series-1: #5b3fd6;
}
```

## Meters

A grant's balance meter fills with `--bs-primary`, turns `--bs-warning` when under a tenth is left, and `--bs-danger` when overdrawn. Each state also carries an icon and a label.

## Notes

- Class names are prefixed `cbw-`.
- Below 768px the grant list and event table become stacked cards.
