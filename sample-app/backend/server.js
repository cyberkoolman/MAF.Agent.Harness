const express = require('express');
const cors = require('cors');
const usersRouter = require('./routes/users');
const tasksRouter = require('./routes/tasks');

const app = express();
const PORT = process.env.PORT || 4000;

app.use(cors());
app.use(express.json());

// Health check
app.get('/api/health', (req, res) => {
  res.json({ status: 'ok', uptime: process.uptime() });
});

// Routes
app.use('/api/users', usersRouter);
app.use('/api/tasks', tasksRouter);

// Error handler
app.use((err, req, res, next) => {
  console.error('Unhandled error:', err.stack);
  res.status(500).json({ error: 'Internal server error' });
});

if (require.main === module) {
  app.listen(PORT, () => {
    console.log(`Team Dashboard API running on http://localhost:${PORT}`);
  });
}

module.exports = app;
