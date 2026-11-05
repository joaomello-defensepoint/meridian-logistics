import { Link as RouterLink, Route, Routes, useLocation } from 'react-router-dom'
import {
  AppBar,
  Box,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
} from '@mui/material'
import LocalShippingIcon from '@mui/icons-material/LocalShipping'
import DashboardIcon from '@mui/icons-material/Dashboard'
import SmartToyIcon from '@mui/icons-material/SmartToy'
import Dashboard from './pages/Dashboard'
import Assistant from './pages/Assistant'

const DRAWER_WIDTH = 240

const navItems = [
  { label: 'Dashboard', path: '/', icon: <DashboardIcon /> },
  { label: 'AI Assistant', path: '/assistant', icon: <SmartToyIcon /> },
]

export default function App() {
  const location = useLocation()

  return (
    <Box sx={{ display: 'flex' }}>
      <AppBar
        position="fixed"
        sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}
      >
        <Toolbar>
          <LocalShippingIcon sx={{ mr: 1.5 }} />
          <Typography variant="h6" noWrap component="div">
            Meridian Logistics
          </Typography>
        </Toolbar>
      </AppBar>

      <Drawer
        variant="permanent"
        sx={{
          width: DRAWER_WIDTH,
          flexShrink: 0,
          '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        <List>
          {navItems.map((item) => (
            <ListItemButton
              key={item.path}
              component={RouterLink}
              to={item.path}
              selected={location.pathname === item.path}
            >
              <ListItemIcon>{item.icon}</ListItemIcon>
              <ListItemText primary={item.label} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, p: 3, minHeight: '100vh' }}>
        <Toolbar />
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/assistant" element={<Assistant />} />
        </Routes>
      </Box>
    </Box>
  )
}
