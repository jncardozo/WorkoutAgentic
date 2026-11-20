import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { NavigationEnd, NavigationError, Router } from '@angular/router';
import { startStartupScreen } from './app/core/startup-screen';

const startup = startStartupScreen(document, window);

bootstrapApplication(App, appConfig)
  .then(app => {
    const router = app.injector.get(Router);
    if (router.navigated) startup.ready();
    else {
      const navigation = router.events.subscribe(event => {
        if (event instanceof NavigationEnd) {
          navigation.unsubscribe();
          startup.ready();
        } else if (event instanceof NavigationError) {
          navigation.unsubscribe();
          startup.fail();
        }
      });
    }
  })
  .catch(error => {
    console.error(error);
    startup.fail();
  });
