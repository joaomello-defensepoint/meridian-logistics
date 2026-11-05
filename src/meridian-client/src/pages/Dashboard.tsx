import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Grid,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material'
import RefreshIcon from '@mui/icons-material/Refresh'
import { api, type Pickup, type Shipment } from '../api'
import { statusColor } from '../theme'

function KpiCard({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <Card elevation={2}>
      <CardContent>
        <Typography variant="overline" color="text.secondary">
          {label}
        </Typography>
        <Typography variant="h4" sx={{ fontWeight: 700, color: 'primary.main' }}>
          {value}
        </Typography>
        {hint && (
          <Typography variant="caption" color="text.secondary">
            {hint}
          </Typography>
        )}
      </CardContent>
    </Card>
  )
}

export default function Dashboard() {
  const [shipments, setShipments] = useState<Shipment[] | null>(null)
  const [pickups, setPickups] = useState<Pickup[]>([])
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    api
      .shipments()
      .then(setShipments)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : 'Failed to load shipments'))
    api.pickups().then(setPickups).catch(() => {})
  }, [])

  useEffect(() => {
    load()
  }, [load])

  const kpis = useMemo(() => {
    if (!shipments) return null
    const active = shipments.filter((s) => s.status !== 'Delivered').length
    const delivered = shipments.filter((s) => s.status === 'Delivered').length
    const onTime = shipments.filter((s) => s.status !== 'Delayed' && s.status !== 'Customs Hold').length
    const onTimeRate = shipments.length ? Math.round((onTime / shipments.length) * 100) : 0
    return { active, delivered, onTimeRate }
  }, [shipments])

  return (
    <Box>
      <Stack
        direction="row"
        sx={{ mb: 2, alignItems: 'center', justifyContent: 'space-between' }}
      >
        <Typography variant="h5">Operations Dashboard</Typography>
        <Button size="small" startIcon={<RefreshIcon />} onClick={load}>
          Refresh
        </Button>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {!shipments && !error && (
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
          <CircularProgress />
        </Box>
      )}

      {kpis && (
        <Grid container spacing={2} sx={{ mb: 3 }}>
          <Grid size={{ xs: 12, md: 4 }}>
            <KpiCard label="Active Shipments" value={String(kpis.active)} hint={`${kpis.delivered} delivered`} />
          </Grid>
          <Grid size={{ xs: 12, md: 4 }}>
            <KpiCard label="On-time Rate" value={`${kpis.onTimeRate}%`} hint="excludes delays & customs holds" />
          </Grid>
          <Grid size={{ xs: 12, md: 4 }}>
            <KpiCard label="Scheduled Pickups" value={String(pickups.length)} hint="booked by the Operations Agent" />
          </Grid>
        </Grid>
      )}

      {shipments && (
        <TableContainer component={Paper} elevation={2}>
          <Table>
            <TableHead>
              <TableRow sx={{ '& th': { fontWeight: 700 } }}>
                <TableCell>Tracking #</TableCell>
                <TableCell>Route</TableCell>
                <TableCell>Service</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>ETA</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {shipments.map((s) => (
                <TableRow key={s.trackingNumber} hover>
                  <TableCell sx={{ fontFamily: 'monospace' }}>{s.trackingNumber}</TableCell>
                  <TableCell>
                    {s.origin} &rarr; {s.destination}
                  </TableCell>
                  <TableCell>{s.service}</TableCell>
                  <TableCell>
                    <Chip label={s.status} color={statusColor(s.status)} size="small" />
                  </TableCell>
                  <TableCell>{s.eta}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {shipments && (
        <Box sx={{ mt: 4 }}>
          <Typography variant="h6" gutterBottom>
            Scheduled Pickups
          </Typography>
          {pickups.length === 0 ? (
            <Alert severity="info" variant="outlined">
              No pickups scheduled yet — ask the Operations Agent to schedule one.
            </Alert>
          ) : (
            <TableContainer component={Paper} elevation={2}>
              <Table>
                <TableHead>
                  <TableRow sx={{ '& th': { fontWeight: 700 } }}>
                    <TableCell>Confirmation</TableCell>
                    <TableCell>Address</TableCell>
                    <TableCell>Date</TableCell>
                    <TableCell>Window</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {pickups.map((p) => (
                    <TableRow key={p.confirmationId} hover>
                      <TableCell sx={{ fontFamily: 'monospace' }}>{p.confirmationId}</TableCell>
                      <TableCell>{p.address}</TableCell>
                      <TableCell>{p.date}</TableCell>
                      <TableCell>{p.timeWindow}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </Box>
      )}
    </Box>
  )
}
