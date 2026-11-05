import { createTheme } from '@mui/material/styles'

// Corporate navy/blue theme on a light background.
export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: '#0b2545', light: '#13315c', dark: '#06172e' },
    secondary: { main: '#1d7fd1' },
    background: { default: '#f4f6fa', paper: '#ffffff' },
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: '"Inter", "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
    h5: { fontWeight: 700 },
    h6: { fontWeight: 700 },
  },
})

// Map a shipment status to an MUI chip color.
export function statusColor(
  status: string,
): 'success' | 'info' | 'warning' | 'error' | 'default' {
  switch (status) {
    case 'Delivered':
      return 'success'
    case 'Out for Delivery':
    case 'In Transit':
      return 'info'
    case 'Scheduled':
      return 'default'
    case 'Delayed':
      return 'warning'
    case 'Customs Hold':
      return 'error'
    default:
      return 'default'
  }
}
