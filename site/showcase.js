(() => {
  'use strict';
  const root = document.documentElement;
  const header = document.querySelector('.header');
  const motionButton = document.getElementById('motion-toggle');
  const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
  let savedMotion = null;
  try { savedMotion = localStorage.getItem('padrevive.motion'); } catch (_) {}
  let reducedMotion = savedMotion === 'off' || (savedMotion !== 'on' && motionPreference.matches);
  const setMotion = (reduced, save = false) => {
    reducedMotion = reduced;
    root.classList.toggle('reduced-motion', reduced);
    root.classList.toggle('motion-enabled', !reduced);
    motionButton.textContent = reduced ? 'Motion off' : 'Motion on';
    motionButton.setAttribute('aria-pressed', String(!reduced));
    motionButton.setAttribute('aria-label', reduced ? 'Turn animations on' : 'Turn animations off');
    if (save) { try { localStorage.setItem('padrevive.motion', reduced ? 'off' : 'on'); } catch (_) {} }
  };
  setMotion(reducedMotion);
  motionButton.hidden = false;
  motionButton.addEventListener('click', () => setMotion(!reducedMotion, true));
  motionPreference.addEventListener('change', (event) => {
    let stored = null;
    try { stored = localStorage.getItem('padrevive.motion'); } catch (_) {}
    if (stored === null) setMotion(event.matches);
  });

  const stories = Array.from(document.querySelectorAll('[data-story]')).map((element) => ({
    element,
    name: element.dataset.story,
    stage: element.querySelector('.story-stage'),
    copies: Array.from(element.querySelectorAll('[data-copy]')),
    screens: Array.from(element.querySelectorAll('[data-screen]')),
    highlights: Array.from(element.querySelectorAll('[data-highlight]')),
    buttons: Array.from(element.querySelectorAll('[data-story-jump]')),
    counter: element.querySelector('[data-current-step]'),
    art: element.querySelector('.anatomy-art'),
    active: -1
  }));
  const anatomyLabels = ['YOUR BUTTONS. YOUR MAPPING.', 'FIND YOUR CENTRE. FIND YOUR FEEL.', 'BRING THE FEEDBACK BACK.', 'CONNECT. CHOOSE. PLAY.'];
  const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
  const selectStep = (story, step) => {
    if (step === story.active) return;
    story.active = step;
    story.copies.forEach((copy, index) => {
      copy.classList.toggle('is-active', index === step);
      copy.setAttribute('aria-hidden', String(index !== step));
    });
    story.screens.forEach((screen, index) => {
      screen.classList.toggle('is-active', index === step);
      screen.setAttribute('aria-hidden', String(index !== step));
    });
    story.highlights.forEach((highlight, index) => highlight.classList.toggle('is-active', index === step));
    story.buttons.forEach((button, index) => button.setAttribute('aria-pressed', String(index === step)));
    story.counter.textContent = String(step + 1).padStart(2, '0');
    if (story.name === 'studio') {
      const image = story.screens[step].querySelector('img');
      document.getElementById('full-screenshot').href = image.getAttribute('src');
    }
    if (story.art) {
      story.art.dataset.anatomyStep = String(step);
      story.art.querySelector('[data-anatomy-label]').textContent = anatomyLabels[step];
    }
  };
  let queued = false;
  const renderScroll = () => {
    queued = false;
    const headerHeight = header.offsetHeight;
    const total = Math.max(1, root.scrollHeight - window.innerHeight);
    document.querySelector('.page-progress span').style.transform = `scaleX(${clamp(window.scrollY / total, 0, 1)})`;
    stories.forEach((story) => {
      const rectangle = story.element.getBoundingClientRect();
      const distance = Math.max(1, story.element.offsetHeight - story.stage.offsetHeight);
      const progress = clamp((headerHeight - rectangle.top) / distance, 0, 1);
      const step = Math.min(story.copies.length - 1, Math.floor(progress * story.copies.length));
      selectStep(story, step);
      story.stage.style.setProperty('--story-progress', String(progress));
      if (story.art) story.art.style.setProperty('--pad-angle', `${-3 + progress * 6}deg`);
    });
  };
  const queueScroll = () => {
    if (queued) return;
    queued = true;
    requestAnimationFrame(renderScroll);
  };
  document.querySelectorAll('[data-story-jump]').forEach((button) => {
    button.addEventListener('click', () => {
      const story = stories.find((item) => item.name === button.dataset.storyJump);
      if (!story) return;
      const step = Number(button.dataset.index);
      const headerHeight = header.offsetHeight;
      const distance = Math.max(1, story.element.offsetHeight - story.stage.offsetHeight);
      const top = story.element.getBoundingClientRect().top + window.scrollY - headerHeight;
      const progress = (step + 0.2) / story.copies.length;
      window.scrollTo({ top: Math.max(0, top + distance * progress), behavior: reducedMotion ? 'instant' : 'smooth' });
    });
  });
  root.classList.add('enhanced');
  window.addEventListener('scroll', queueScroll, { passive: true });
  window.addEventListener('resize', queueScroll, { passive: true });
  window.addEventListener('pageshow', queueScroll);

  const reveals = document.querySelectorAll('.reveal');
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-visible');
        observer.unobserve(entry.target);
      });
    }, { threshold: 0.12 });
    reveals.forEach((element) => observer.observe(element));
  } else {
    reveals.forEach((element) => element.classList.add('is-visible'));
  }
  const heroArt = document.querySelector('.hero-art');
  const heroController = document.querySelector('.hero-controller');
  heroArt.addEventListener('pointermove', (event) => {
    if (reducedMotion || event.pointerType !== 'mouse') return;
    const bounds = heroArt.getBoundingClientRect();
    const x = (event.clientX - bounds.left) / bounds.width - 0.5;
    const y = (event.clientY - bounds.top) / bounds.height - 0.5;
    heroController.style.setProperty('--tilt-x', `${-y * 7}deg`);
    heroController.style.setProperty('--tilt-y', `${x * 7}deg`);
  });
  heroArt.addEventListener('pointerleave', () => {
    heroController.style.setProperty('--tilt-x', '0deg');
    heroController.style.setProperty('--tilt-y', '0deg');
  });
  renderScroll();
})();
