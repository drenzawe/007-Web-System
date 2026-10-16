import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ServicesPage } from './services';

describe('ServicesPage', () => {
  let fixture: ComponentFixture<ServicesPage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ServicesPage],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(ServicesPage);
    await fixture.whenStable();
  });

  it('shows the main retrofit and customization services', () => {
    const content = fixture.nativeElement.textContent as string;

    expect(content).toContain('Bi-LED projector retrofit');
    expect(content).toContain('DRL upgrades');
    expect(content).toContain('Angel Eyes & Devil Eyes');
    expect(content).toContain('Turn signals & fog lights');
    expect(content).toContain('Montero owners');
  });

  it('links service enquiries to booking and the shop Facebook page', () => {
    const links = fixture.nativeElement.querySelectorAll('a') as NodeListOf<HTMLAnchorElement>;

    expect(Array.from(links).some((link) => link.getAttribute('routerLink') === '/schedule')).toBe(true);
    expect(Array.from(links).some((link) =>
      link.href === 'https://www.facebook.com/007HeadlightRetrofit',
    )).toBe(true);
  });

  it('shows all the project example videos with playback controls', () => {
    const videos = fixture.nativeElement.querySelectorAll('video') as NodeListOf<HTMLVideoElement>;

    expect(videos).toHaveLength(5);
    expect(Array.from(videos).map((video) =>
      video.querySelector('source')?.getAttribute('src'),
    )).toEqual(['/sample0.mp4', '/sample1.mp4', '/sample2.mp4', '/sample3.mp4', '/sample4.mp4']);
    expect(Array.from(videos).every((video) =>
      video.controls && video.hasAttribute('playsinline'),
    )).toBe(true);
  });
});
