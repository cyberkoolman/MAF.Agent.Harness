const express = require('express');
const router = express.Router();
const path = require('path');

function loadDb() {
  return require(path.join(__dirname, '..', 'data', 'db.json'));
}

// GET /api/tasks — list all tasks
router.get('/', (req, res) => {
  const db = loadDb();
  const tasks = db.tasks.map(task => {
    const assignee = db.users.find(u => u.id === task.assignee);
    return {
      ...task,
      assigneeName: assignee ? assignee.name : 'Unassigned'
    };
  });
  res.json(tasks);
});

// GET /api/tasks/:id — get a single task
router.get('/:id', (req, res) => {
  const db = loadDb();
  const task = db.tasks.find(t => t.id === parseInt(req.params.id));
  if (!task) {
    return res.status(404).json({ error: 'Task not found' });
  }
  const assignee = db.users.find(u => u.id === task.assignee);
  res.json({ ...task, assigneeName: assignee ? assignee.name : 'Unassigned' });
});

module.exports = router;
