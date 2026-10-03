
async function api(url, options={}) {
  const r = await fetch(url, {
    headers: {'Content-Type':'application/json', ...(options.headers||{})},
    ...options
  });
  if (!r.ok) {
    let msg = 'Request failed';
    try { const e=await r.json(); msg=e.message||msg; } catch {}
    throw new Error(msg);
  }
  if (r.status===204) return null;
  return r.json();
}

function esc(v){
  return String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
}

async function loadDashboard(){
  if (typeof loadDashboardData === 'function') {
    return loadDashboardData();
  }
  try {
    const d = await api('/api/dashboard');
    document.querySelector('#totalMembers')?.replaceChildren(d.members);
    document.querySelector('#totalTrainers')?.replaceChildren(d.trainers);
    document.querySelector('#activeMemberships')?.replaceChildren(d.activeMemberships);
    document.querySelector('#totalPayments')?.replaceChildren('$'+Number(d.totalPayments).toFixed(2));
  } catch(e){}
}

async function loadMembers(){
  // If the page defines its own comprehensive member loader, delegate to it
  if (window.location.pathname.toLowerCase().includes('/admin/members')) {
    return;
  }
  const rows = document.querySelector('#membersRows'); 
  if(!rows) return;
  try {
    const data = await api('/api/members');
    rows.innerHTML = data.map(x=>`<tr><td>#${x.id}</td><td><strong>${esc(x.fullName)}</strong></td><td>${esc(x.phone || '-')}</td><td>${esc(x.registrationDate)}</td><td>${esc(x.expiryDate)}</td><td><span class="badge">${esc(x.status)}</span></td></tr>`).join('');
  } catch(e){}
}

async function loadTrainers(){
  const rows = document.querySelector('#trainersRows');
  if(!rows) return;
  try {
    const data = await api('/api/trainers');
    rows.innerHTML = data.map(x=>`<tr><td>#${x.id}</td><td><strong>${esc(x.fullName)}</strong></td><td>${esc(x.email)}</td><td>${esc(x.phone)}</td><td>${esc(x.specialization)}</td></tr>`).join('');
  } catch(e){}
}

async function addTrainer(e){
  e.preventDefault();
  const f=e.target;
  await api('/api/trainers',{
    method:'POST',
    body:JSON.stringify({
      fullName:f.fullName.value,
      email:f.email.value,
      phone:f.phone.value,
      specialization:f.specialization.value
    })
  });
  f.reset();
  loadTrainers();
  alert('Tababare cusub waa lagu daray!');
}

async function loadPayments(){
  if (window.location.pathname.toLowerCase().includes('/admin/payments')) {
    return;
  }
  const rows = document.querySelector('#paymentsRows');
  if(!rows) return;
  try {
    const data = await api('/api/payments');
    rows.innerHTML = data.map(x=>`<tr><td>${x.invoiceNumber||('INV-'+x.id)}</td><td>${esc(x.member?.fullName||x.memberId)}</td><td>$${Number(x.amount).toFixed(2)}</td><td>${esc(x.paymentMethod)}</td><td>${new Date(x.paymentDate).toLocaleDateString()}</td></tr>`).join('');
  } catch(e){}
}

async function loadReports(){
  const summaryEl = document.querySelector('#reportMembers');
  if (!summaryEl) return;
  try {
    const d = await api('/api/reports/summary');
    const mapping = {
      reportMembers: d.members,
      reportActive: d.activeMemberships,
      reportExpired: d.expiredMemberships,
      reportIncome: '$' + Number(d.monthlyIncome).toFixed(2)
    };
    for (const [id, val] of Object.entries(mapping)) {
      document.querySelector('#' + id)?.replaceChildren(val);
    }
  } catch(e){}
}

document.addEventListener('DOMContentLoaded', () => {
  loadDashboard();
  loadMembers();
  loadTrainers();
  loadPayments();
  loadReports();
});
