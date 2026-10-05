import { alpha, createTheme } from '@mui/material/styles';

/**
 * ProCargo visual language — taken from the Indian highway:
 *  - lorry teal: the paint of South Indian goods carriers, used for primary actions and navigation;
 *  - commercial-plate yellow: the yellow number plate every goods vehicle carries; reserved for one thing only,
 *    the "plate" tags that show booking, trip and vehicle numbers (see PlateTag);
 *  - asphalt: text and dark surfaces; milestone white: the page.
 * Archivo is used throughout; its condensed width gives headings the lettering of a truck's tailboard.
 */
export const palette = {
  teal: '#0F5F5B',
  tealDark: '#0A4441',
  tealTint: '#E3EFEE',
  plate: '#F5C518',
  asphalt: '#1E2328',
  slate: '#5A636B',
  milestone: '#F6F7F5',
  line: '#DCE0DD',
  red: '#B3261E',
  green: '#2E7D32',
  amber: '#9A6700',
};

export const displayFont = '"Archivo", "Segoe UI", system-ui, sans-serif';

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: palette.teal, dark: palette.tealDark, contrastText: '#FFFFFF' },
    secondary: { main: palette.asphalt },
    error: { main: palette.red },
    success: { main: palette.green },
    warning: { main: palette.amber },
    background: { default: palette.milestone, paper: '#FFFFFF' },
    text: { primary: palette.asphalt, secondary: palette.slate },
    divider: palette.line,
  },
  shape: { borderRadius: 6 },
  typography: {
    fontFamily: displayFont,
    fontSize: 15,
    h1: { fontSize: 'clamp(2.4rem, 5vw, 4rem)', fontWeight: 800, fontStretch: '75%', lineHeight: 1.02, letterSpacing: '-0.01em' },
    h2: { fontSize: 'clamp(1.8rem, 3.4vw, 2.6rem)', fontWeight: 800, fontStretch: '75%', lineHeight: 1.08 },
    h3: { fontSize: '1.6rem', fontWeight: 750, fontStretch: '80%', lineHeight: 1.15 },
    h4: { fontSize: '1.35rem', fontWeight: 700, fontStretch: '85%' },
    h5: { fontSize: '1.15rem', fontWeight: 700 },
    h6: { fontSize: '1rem', fontWeight: 700 },
    subtitle1: { fontWeight: 600 },
    body1: { lineHeight: 1.6 },
    body2: { lineHeight: 1.55 },
    button: { textTransform: 'none', fontWeight: 650, letterSpacing: 0 },
    overline: { textTransform: 'none', letterSpacing: 0, fontWeight: 600, fontSize: '0.8rem' },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: { fontVariantNumeric: 'tabular-nums' },
        '*:focus-visible': { outline: `3px solid ${palette.plate}`, outlineOffset: 2 },
        '@media (prefers-reduced-motion: reduce)': {
          '*': { animationDuration: '0.01ms !important', transitionDuration: '0.01ms !important' },
        },
      },
    },
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: { root: { borderRadius: 6, paddingInline: 18 }, sizeLarge: { paddingBlock: 12, fontSize: '1rem' } },
    },
    MuiPaper: {
      defaultProps: { elevation: 0 },
      styleOverrides: { outlined: { borderColor: palette.line } },
    },
    MuiCard: { defaultProps: { variant: 'outlined' } },
    MuiTextField: { defaultProps: { fullWidth: true, size: 'small' } },
    MuiTableCell: {
      styleOverrides: {
        head: { fontWeight: 700, color: palette.slate, fontSize: '0.82rem', backgroundColor: '#FAFBFA' },
      },
    },
    MuiChip: { styleOverrides: { root: { fontWeight: 600 } } },
    MuiAppBar: { defaultProps: { elevation: 0, color: 'inherit' } },
    MuiListItemButton: {
      styleOverrides: {
        root: {
          borderRadius: 6,
          '&.Mui-selected': { backgroundColor: alpha(palette.teal, 0.1), color: palette.tealDark, fontWeight: 700 },
        },
      },
    },
  },
});
