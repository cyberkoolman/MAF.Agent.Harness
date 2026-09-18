import React from 'react';

function Navbar({ items, currentPage, onNavigate }) {
  return (
    <nav className="navbar">
      <div className="navbar-brand">
        <span className="navbar-logo">MC</span>
        <h1 className="navbar-title">Mission Control</h1>
      </div>

      <ul className="navbar-links">
        {items.map(item => (
          <li key={item.id} className={`navbar-link ${currentPage === item.id ? 'active' : ''}`}>
            <button onClick={() => onNavigate(item.id)}>
              {item.label}
            </button>
          </li>
        ))}
      </ul>

      <div className="navbar-status">
        <span className="status-dot"></span>
        <span className="status-text">System Online</span>
      </div>
    </nav>
  );
}

export default Navbar;
